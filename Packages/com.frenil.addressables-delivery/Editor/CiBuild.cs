using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace AddressablesDelivery.Editor
{
    /// <summary>
    /// CI 배치 진입점. 빌드 창과 같은 정적 메서드를 부르고 결과를 종료 코드로 돌려준다(0 성공, 1 실패).
    /// <c>Unity -batchmode -nographics -projectPath . -buildTarget Android -executeMethod AddressablesDelivery.Editor.CiBuild.Addressables -deliveryMode update</c>
    /// 처럼 부른다. 메서드가 직접 종료하므로 <c>-quit</c> 는 붙이지 않는다. 플랫폼은 <c>-buildTarget</c> 으로 연다.
    /// </summary>
    public static class CiBuild
    {
        private const string LogPrefix = "[CI]";

        /// <summary>배지를 옮긴 뒤 공개 주소에 반영되기까지 걸리는 시간을 기다리는 횟수와 간격(CCD 에서 1~2분 측정).</summary>
        private const int RemoteAttempts = 24;
        private const int RemoteIntervalMilliseconds = 10000;

        /// <summary><c>-deliveryProfile 이름</c>(기본: 원격 프로필), <c>-deliveryMode full|update</c>(기본 update).</summary>
        public static void Addressables() => Run(() =>
        {
            if (!AddressablesBuild.UseProfile(Arg("deliveryProfile", AddressablesBuild.RemoteProfile)))
                return false;

            return Arg("deliveryMode", "update") switch
            {
                "full" => AddressablesBuild.BuildFull(),
                "update" => AddressablesBuild.BuildContentUpdate(),
                var mode => Fail($"알 수 없는 -deliveryMode {mode}"),
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
        /// 원격 저장소에 올린 뒤, 공개 주소의 카탈로그 해시가 올린 것과 같은지, 올린 파일이 모두 같은 크기로 있는지 확인한다.
        /// <c>-deliveryBadge 이름|none</c>(선택), <c>-deliveryNotes 메모</c>(선택). 키는 CI 가 환경 변수로 넣는다.
        /// </summary>
        public static void Upload() => Run(() =>
        {
            var badge = Arg("deliveryBadge", null);
            if (badge == "none" || badge == string.Empty)
                badge = null;

            if (!ContentUpload.Upload(Arg("deliveryNotes", $"CI {ContentUpload.DefaultReleaseNotes}"), badge))
                return false;

            // CCD 는 출시 배지(latest)와 붙인 배지를 모두 본다. Custom 은 배지가 없어 한 번만 본다.
            var isCcd = DeliverySettings.Current.Storage == RemoteStorage.Ccd;
            if (!VerifyRemoteCatalog(isCcd ? CcdLocation.ReleaseBadge : null))
                return false;

            if (isCcd && badge != null && !VerifyRemoteCatalog(badge))
                return false;

            return VerifyRemoteFiles(isCcd ? badge ?? CcdLocation.ReleaseBadge : null);
        });

        /// <summary>
        /// 원격 빌드 폴더의 파일(카탈로그, 번들)이 공개 주소에 모두 있고 크기가 같은지 본다.
        /// 카탈로그 해시가 같아도 번들이 빠지면 받는 쪽에서만 실패하므로 업로드 직후 여기서 막는다.
        /// </summary>
        private static bool VerifyRemoteFiles(string badge)
        {
            var files = Directory.GetFiles(ContentUpload.RemoteBuildFolder);
            var problems = new List<string>();
            long totalBytes = 0;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            foreach (var path in files)
            {
                var name = Path.GetFileName(path);
                var expected = new FileInfo(path).Length;
                totalBytes += expected;
                try
                {
                    using var response = http.GetAsync(ContentUpload.FileUrl(name, badge), HttpCompletionOption.ResponseHeadersRead)
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
                return Fail($"원격 파일 검증 실패({badge ?? "-"}) {problems.Count}/{files.Length}개: {string.Join("; ", problems)}");

            Debug.Log($"{LogPrefix} 원격 파일 검증 통과({badge ?? "-"}): {files.Length}개, {totalBytes} bytes");
            return true;
        }

        /// <summary>공개 주소의 카탈로그 해시를 받아 로컬 빌드 결과와 비교한다. 반영 지연이 있어 간격을 두고 다시 받는다.</summary>
        private static bool VerifyRemoteCatalog(string badge)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var fileName = $"catalog_{settings.PlayerBuildVersion}.hash";
            var expected = File.ReadAllText(Path.Combine(ContentUpload.RemoteBuildFolder, fileName)).Trim();
            var url = ContentUpload.FileUrl(fileName, badge);
            var label = badge ?? "-";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            for (var attempt = 1; attempt <= RemoteAttempts; attempt++)
            {
                try
                {
                    var remote = http.GetStringAsync(url).GetAwaiter().GetResult().Trim();
                    if (remote == expected)
                    {
                        Debug.Log($"{LogPrefix} 원격 검증 통과({label}): {remote}");
                        return true;
                    }

                    Debug.Log($"{LogPrefix} 원격 해시가 아직 다르다({label}, {attempt}회): {remote} != {expected}");
                }
                catch (Exception e)
                {
                    Debug.Log($"{LogPrefix} 원격 해시를 받지 못했다({label}, {attempt}회): {e.GetBaseException().Message}");
                }

                Thread.Sleep(RemoteIntervalMilliseconds);
            }

            return Fail($"원격 검증 실패({label}): {url}");
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
