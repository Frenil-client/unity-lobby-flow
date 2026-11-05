using System;
using System.Diagnostics;
using System.IO;
using LobbyFlow.Patching;
using UnityEditor;
using UnityEditor.AddressableAssets;
using Debug = UnityEngine.Debug;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// 원격 빌드 폴더(<c>ServerData/[BuildTarget]</c>)를 현재 플랫폼의 CCD 버킷에 올리고 릴리스를 만든다(SPEC 5.5).
    /// UGS CLI(<c>ugs ccd entries sync -r</c>)를 그대로 호출하므로 Jenkins 도 같은 명령을 쓴다.
    /// 인증은 서비스 계정이다. 키는 환경 변수(UGS_CLI_SERVICE_KEY_ID, UGS_CLI_SERVICE_SECRET_KEY)나 <c>ugs login</c> 으로 주고 저장소에 두지 않는다.
    /// </summary>
    public static class CcdUpload
    {
        private const string LogPrefix = "[CCD]";
        private const int TimeoutMilliseconds = 10 * 60 * 1000;

        public static string DefaultReleaseNotes => $"Lobby Flow {DateTime.Now:yyyy-MM-dd HH:mm}";

        /// <summary>업로드 권한이 있는지. 서비스 계정 키는 작성자 PC 의 환경 변수에만 있다(받은 사람은 다운로드만 한다).</summary>
        public static bool HasServiceKey =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UGS_CLI_SERVICE_KEY_ID")) &&
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UGS_CLI_SERVICE_SECRET_KEY"));

        /// <param name="badge">새 릴리스에 붙일 콘텐츠 버전 배지(v1, v2). 비우면 CCD 가 옮기는 latest 만 붙는다.</param>
        /// <returns>성공하면 true.</returns>
        public static bool UploadAndRelease(string releaseNotes, string badge = null)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"{LogPrefix} Addressables 설정이 없다.");
                return false;
            }

            // LocalServer 프로필로 빌드한 카탈로그는 localhost 를 가리키므로 올려도 기기에서 쓸 수 없다.
            if (AddressablesBuild.ActiveProfileName != AddressablesBuild.CcdProfile)
            {
                Debug.LogError($"{LogPrefix} 현재 프로필이 {AddressablesBuild.ActiveProfileName} 이다. CCD 프로필로 바꾸고 전체 빌드를 다시 한 뒤 올린다.");
                return false;
            }

            var bucket = CcdConfig.Current;
            if (string.IsNullOrEmpty(CcdConfig.ProjectId) || string.IsNullOrEmpty(bucket.Id))
            {
                Debug.LogError($"{LogPrefix} CcdConfig 의 프로젝트 ID 또는 {bucket.Name} 버킷 ID 가 비어 있다.");
                return false;
            }

            var folder = Path.GetFullPath(settings.RemoteCatalogBuildPath.GetValue(settings));
            if (!Directory.Exists(folder))
            {
                Debug.LogError($"{LogPrefix} 원격 빌드 폴더가 없다: {folder}. Addressables 빌드를 먼저 한다.");
                return false;
            }

            try
            {
                // 지우기(-x)는 쓰지 않는다. 이전 카탈로그를 가진 플레이어가 아직 이전 번들을 받을 수 있어야 한다.
                EditorUtility.DisplayProgressBar("CCD", $"{bucket.Name} 에 올리고 릴리스 만드는 중", 0.5f);
                var arguments = $"ccd entries sync \"{folder}\" -p {CcdConfig.ProjectId} -e {CcdConfig.Environment} -b {bucket.Name} -r -n \"{releaseNotes}\" -j";
                if (!string.IsNullOrEmpty(badge))
                    arguments += $" -u {badge}";

                if (!RunUgs(arguments, out var output))
                    return false;

                Debug.Log($"{LogPrefix} {bucket.Name} 업로드와 릴리스 완료({CcdConfig.Environment}{(string.IsNullOrEmpty(badge) ? "" : $", 배지 {badge}")}).\n{output}");
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool RunUgs(string arguments, out string output)
        {
            var start = new ProcessStartInfo("ugs", arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // 키(UGS_CLI_SERVICE_KEY_ID, UGS_CLI_SERVICE_SECRET_KEY)는 사용자 환경 변수에서 그대로 물려받는다.
            Debug.Log($"{LogPrefix} ugs {arguments}");
            try
            {
                using var process = Process.Start(start);
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(TimeoutMilliseconds))
                {
                    process.Kill();
                    output = stdout.Result;
                    Debug.LogError($"{LogPrefix} 시간 초과: ugs {arguments}");
                    return false;
                }

                output = stdout.Result.Trim();
                if (process.ExitCode != 0)
                {
                    Debug.LogError($"{LogPrefix} 실패({process.ExitCode}): ugs {arguments}\n{stderr.Result.Trim()}\n{output}");
                    return false;
                }

                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                output = string.Empty;
                Debug.LogError($"{LogPrefix} ugs 를 찾지 못했다. UGS CLI 를 설치하고 PATH 에 넣은 뒤 Unity 를 다시 연다.");
                return false;
            }
        }
    }
}
