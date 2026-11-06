using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using LobbyFlow.Patching;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// Jenkins 배치 진입점(SPEC 8). 빌드 창과 같은 정적 메서드를 부르고 결과를 종료 코드로 돌려준다(0 성공, 1 실패).
    /// <c>Unity -batchmode -nographics -projectPath . -buildTarget Android -executeMethod LobbyFlow.Editor.CiBuild.Addressables -lobbyMode update</c>
    /// 처럼 부른다. 메서드가 직접 종료하므로 <c>-quit</c> 는 붙이지 않는다. 플랫폼은 <c>-buildTarget</c> 으로 연다.
    /// </summary>
    public static class CiBuild
    {
        private const string LogPrefix = "[CI]";

        /// <summary><c>-lobbyProfile CCD|LocalServer</c>(기본 CCD), <c>-lobbyMode full|update</c>(기본 update).</summary>
        public static void Addressables() => Run(() =>
        {
            if (!AddressablesBuild.UseProfile(Arg("lobbyProfile", AddressablesBuild.CcdProfile)))
                return false;

            return Arg("lobbyMode", "update") switch
            {
                "full" => AddressablesBuild.BuildFull(),
                "update" => AddressablesBuild.BuildContentUpdate(),
                var mode => Fail($"알 수 없는 -lobbyMode {mode}"),
            };
        });

        /// <summary>현재 플랫폼의 개발 빌드. 결과는 <c>Builds/</c> 아래에 둔다.</summary>
        public static void Player() => Run(() => EditorUserBuildSettings.activeBuildTarget switch
        {
            BuildTarget.StandaloneWindows64 => PlayerBuild.BuildWindowsDevelopment(out _),
            BuildTarget.Android => PlayerBuild.BuildAndroidDevelopment(false, out _),
            var target => Fail($"{target} 플레이어 빌드는 아직 없다"),
        });

        /// <summary>
        /// 현재 플랫폼 버킷에 올리고 릴리스를 만든 뒤, 공개 주소의 카탈로그 해시가 올린 것과 같은지, 올린 파일이 모두 같은 크기로 있는지 확인한다.
        /// <c>-lobbyBadge v1|v2</c>(선택), <c>-lobbyNotes 메모</c>(선택). 키는 Jenkins Credentials 가 환경 변수로 넣는다.
        /// </summary>
        public static void Upload() => Run(() =>
        {
            var badge = Arg("lobbyBadge", null);
            if (badge == "none")
                badge = null;

            if (!CcdUpload.UploadAndRelease(Arg("lobbyNotes", $"Jenkins {CcdUpload.DefaultReleaseNotes}"), badge))
                return false;

            return VerifyRemoteCatalog(CcdConfig.LatestBadge) && (badge == null || VerifyRemoteCatalog(badge))
                && VerifyRemoteFiles(badge ?? CcdConfig.LatestBadge);
        });

        /// <summary>
        /// 원격 빌드 폴더의 파일(카탈로그, 번들)이 배지가 가리키는 릴리스에 모두 있고 크기가 같은지 본다.
        /// 카탈로그 해시가 같아도 번들이 빠지면 받는 쪽에서만 실패하므로 업로드 직후 여기서 막는다.
        /// </summary>
        private static bool VerifyRemoteFiles(string badge)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var folder = settings.RemoteCatalogBuildPath.GetValue(settings);
            var files = Directory.GetFiles(folder);
            var problems = new System.Collections.Generic.List<string>();
            long totalBytes = 0;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            foreach (var path in files)
            {
                var name = Path.GetFileName(path);
                var expected = new FileInfo(path).Length;
                totalBytes += expected;
                try
                {
                    using var response = http.GetAsync(CcdConfig.EntryUrl(CcdConfig.Current, badge, name), HttpCompletionOption.ResponseHeadersRead)
                        .GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        problems.Add($"{name}: HTTP {(int)response.StatusCode}");
                        continue;
                    }

                    var length = response.Content.Headers.ContentLength
                                 ?? response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult().LongLength;
                    if (length != expected)
                        problems.Add($"{name}: 크기 {length} != {expected}");
                }
                catch (Exception e)
                {
                    problems.Add($"{name}: {e.GetBaseException().Message}");
                }
            }

            if (problems.Count > 0)
                return Fail($"원격 파일 검증 실패({badge}) {problems.Count}/{files.Length}개: {string.Join("; ", problems)}");

            Debug.Log($"{LogPrefix} 원격 파일 검증 통과({badge}): {files.Length}개, {totalBytes} bytes");
            return true;
        }

        /// <summary>
        /// 배지가 가리키는 릴리스의 카탈로그 해시를 받아 로컬 빌드 결과와 비교한다. 배지를 옮긴 뒤 공개 주소에 반영되기까지
        /// 1~2분 걸려(2026-11-05 측정) 10초 간격으로 최대 4분 다시 받는다.
        /// </summary>
        private static bool VerifyRemoteCatalog(string badge)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var fileName = $"catalog_{settings.PlayerBuildVersion}.hash";
            var localPath = Path.Combine(settings.RemoteCatalogBuildPath.GetValue(settings), fileName);
            var expected = File.ReadAllText(localPath).Trim();
            var url = CcdConfig.EntryUrl(CcdConfig.Current, badge, fileName);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            for (var attempt = 1; attempt <= 24; attempt++)
            {
                try
                {
                    var remote = http.GetStringAsync(url).GetAwaiter().GetResult().Trim();
                    if (remote == expected)
                    {
                        Debug.Log($"{LogPrefix} 원격 검증 통과({badge}): {remote}");
                        return true;
                    }

                    Debug.Log($"{LogPrefix} 원격 해시가 아직 다르다({badge}, {attempt}회): {remote} != {expected}");
                }
                catch (Exception e)
                {
                    Debug.Log($"{LogPrefix} 원격 해시를 받지 못했다({badge}, {attempt}회): {e.GetBaseException().Message}");
                }

                Thread.Sleep(10000);
            }

            return Fail($"원격 검증 실패({badge}): {url}");
        }

        private static void Run(Func<bool> step)
        {
            bool ok;
            try
            {
                ok = step();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ok = false;
            }

            Debug.Log($"{LogPrefix} {(ok ? "성공" : "실패")}");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Fail(string message)
        {
            Debug.LogError($"{LogPrefix} {message}");
            return false;
        }

        /// <summary>명령줄의 <c>-이름 값</c>. 없으면 기본값.</summary>
        private static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-" + name)
                    return args[i + 1];
            }

            return fallback;
        }
    }
}
