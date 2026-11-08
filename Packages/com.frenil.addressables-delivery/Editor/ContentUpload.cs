using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AddressablesDelivery.Editor
{
    /// <summary>
    /// 원격 빌드 폴더(<c>ServerData/[BuildTarget]</c>)를 원격 저장소에 올린다.
    /// CCD 는 UGS CLI(<c>ugs ccd entries sync -r</c>)로 현재 플랫폼 버킷에 올리고 릴리스를 만든다. 인증은 서비스 계정 키를
    /// 환경 변수(UGS_CLI_SERVICE_KEY_ID, UGS_CLI_SERVICE_SECRET_KEY)로 받고 저장소에 두지 않는다.
    /// Custom 은 설정 에셋의 업로드 명령을 실행한다. 인증은 그 명령의 도구(aws, rclone 등)가 환경 변수나 자기 설정으로 한다.
    /// </summary>
    public static class ContentUpload
    {
        private const string LogPrefix = "[Upload]";
        private const int TimeoutMilliseconds = 10 * 60 * 1000;

        public static string DefaultReleaseNotes => $"{PlayerSettings.productName} {DateTime.Now:yyyy-MM-dd HH:mm}";

        /// <summary>이 PC 에서 올릴 수 있는지. 못 올리면 <paramref name="reason"/> 에 이유를 둔다. 받는 쪽(다운로드)은 키가 필요 없다.</summary>
        public static bool CanUpload(out string reason)
        {
            var settings = DeliverySettings.Current;
            if (settings == null)
            {
                reason = $"{nameof(DeliverySettings)} 가 없다.";
                return false;
            }

            if (settings.Storage == RemoteStorage.Custom)
            {
                reason = string.IsNullOrWhiteSpace(settings.CustomUploadCommand) ? "설정 에셋에 업로드 명령이 없다." : null;
                return reason == null;
            }

            var hasKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UGS_CLI_SERVICE_KEY_ID")) &&
                         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UGS_CLI_SERVICE_SECRET_KEY"));
            reason = hasKey ? null : "서비스 계정 키(UGS_CLI_SERVICE_KEY_ID, UGS_CLI_SERVICE_SECRET_KEY)가 환경 변수에 없다.";
            return hasKey;
        }

        /// <summary>현재 플랫폼 원격 빌드 폴더. 없으면 null.</summary>
        public static string RemoteBuildFolder
        {
            get
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                return settings != null ? Path.GetFullPath(settings.RemoteCatalogBuildPath.GetValue(settings)) : null;
            }
        }

        /// <summary>
        /// 올린 파일을 받는 공개 주소. CCD 는 배지가 가리키는 릴리스(비우면 출시 배지), Custom 은 원격 프로필 주소 아래 파일이다(배지는 쓰지 않는다).
        /// </summary>
        public static string FileUrl(string fileName, string badge)
        {
            var settings = DeliverySettings.Current;
            if (settings != null && settings.Storage == RemoteStorage.Custom)
            {
                var addressables = AddressableAssetSettingsDefaultObject.Settings;
                return $"{addressables.RemoteCatalogLoadPath.GetValue(addressables).TrimEnd('/')}/{fileName}";
            }

            return CcdLocation.EntryUrl(CcdLocation.Current, string.IsNullOrEmpty(badge) ? CcdLocation.ReleaseBadge : badge, fileName);
        }

        /// <param name="badge">CCD: 새 릴리스에 더 붙일 배지(예: v2). 비우면 CCD 가 옮기는 latest 만 붙는다. Custom: 명령의 {badge} 에 들어간다.</param>
        /// <returns>성공하면 true.</returns>
        public static bool Upload(string releaseNotes, string badge = null)
        {
            var settings = DeliverySettings.Current;
            if (settings == null || AddressableAssetSettingsDefaultObject.Settings == null)
            {
                Debug.LogError($"{LogPrefix} Addressables 설정이나 {nameof(DeliverySettings)} 가 없다. {DeliveryProjectSetup.MenuPath} 을 먼저 실행한다.");
                return false;
            }

            // LocalServer 프로필로 빌드한 카탈로그는 localhost 를 가리키므로 올려도 기기에서 쓸 수 없다.
            if (AddressablesBuild.ActiveProfileName != AddressablesBuild.RemoteProfile)
            {
                Debug.LogError($"{LogPrefix} 현재 프로필이 {AddressablesBuild.ActiveProfileName} 이다. {AddressablesBuild.RemoteProfile} 프로필로 바꾸고 전체 빌드를 다시 한 뒤 올린다.");
                return false;
            }

            var folder = RemoteBuildFolder;
            if (!Directory.Exists(folder))
            {
                Debug.LogError($"{LogPrefix} 원격 빌드 폴더가 없다: {folder}. Addressables 빌드를 먼저 한다.");
                return false;
            }

            try
            {
                EditorUtility.DisplayProgressBar("업로드", $"{Path.GetFileName(folder)} 올리는 중", 0.5f);
                return settings.Storage == RemoteStorage.Custom
                    ? UploadCustom(settings.CustomUploadCommand, folder, releaseNotes, badge)
                    : UploadCcd(folder, releaseNotes, badge);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool UploadCcd(string folder, string releaseNotes, string badge)
        {
            var bucket = CcdLocation.Current;
            if (string.IsNullOrEmpty(CcdLocation.ProjectId) || string.IsNullOrEmpty(bucket.Name))
            {
                Debug.LogError($"{LogPrefix} {nameof(DeliverySettings)} 의 CCD 프로젝트 ID 또는 현재 플랫폼 버킷 이름이 비어 있다.");
                return false;
            }

            // 지우기(-x)는 쓰지 않는다. 이전 카탈로그를 가진 플레이어가 아직 이전 번들을 받을 수 있어야 한다.
            var arguments = $"ccd entries sync \"{folder}\" -p {CcdLocation.ProjectId} -e {CcdLocation.Environment} -b {bucket.Name} -r -n \"{releaseNotes}\" -j";
            if (!string.IsNullOrEmpty(badge))
                arguments += $" -u {badge}";

            // PATH 에 ugs 가 없는 실행 환경(Jenkins 서비스 등)은 UGS_EXE 로 실행 파일 경로를 준다.
            var exe = Environment.GetEnvironmentVariable("UGS_EXE");
            if (!Run(string.IsNullOrEmpty(exe) ? "ugs" : exe, arguments, $"ugs {arguments}", out var output))
                return false;

            Debug.Log($"{LogPrefix} CCD {bucket.Name} 업로드와 릴리스 완료({CcdLocation.Environment}{(string.IsNullOrEmpty(badge) ? "" : $", 배지 {badge}")}).\n{output}");
            return true;
        }

        private static bool UploadCustom(string template, string folder, string releaseNotes, string badge)
        {
            var command = template
                .Replace("{folder}", folder)
                .Replace("{platform}", Path.GetFileName(folder))
                .Replace("{badge}", badge ?? string.Empty)
                .Replace("{notes}", releaseNotes ?? string.Empty);

            var windows = Application.platform == RuntimePlatform.WindowsEditor;
            var shell = windows ? "cmd.exe" : "/bin/sh";
            // cmd 는 /s 를 주면 바깥 따옴표 한 쌍만 벗기고 안의 따옴표는 그대로 둔다.
            var arguments = windows ? $"/s /c \"{command}\"": $"-c \"{command.Replace("\"", "\\\"")}\"";
            if (!Run(shell, arguments, command, out var output))
                return false;

            Debug.Log($"{LogPrefix} 업로드 명령 완료.\n{output}");
            return true;
        }

        private static bool Run(string fileName, string arguments, string display, out string output)
        {
            var start = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // 키는 부모 프로세스(에디터, Jenkins)의 환경 변수에서 그대로 물려받는다. 로그에는 명령만 남긴다.
            Debug.Log($"{LogPrefix} {display}");
            try
            {
                using var process = Process.Start(start);
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(TimeoutMilliseconds))
                {
                    process.Kill();
                    output = stdout.Result;
                    Debug.LogError($"{LogPrefix} 시간 초과: {display}");
                    return false;
                }

                output = stdout.Result.Trim();
                if (process.ExitCode != 0)
                {
                    Debug.LogError($"{LogPrefix} 실패({process.ExitCode}): {display}\n{stderr.Result.Trim()}\n{output}");
                    return false;
                }

                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                output = string.Empty;
                Debug.LogError($"{LogPrefix} {fileName} 를 찾지 못했다. PATH 에 넣거나(ugs 는 UGS_EXE 로 경로 지정) Unity 를 다시 연다.");
                return false;
            }
        }
    }
}
