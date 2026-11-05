using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// 플레이어 빌드(SPEC 5.7). Addressables 콘텐츠는 빌드 메뉴로 따로 만들고, 플레이어에는 그때의 로컬 콘텐츠와 카탈로그가 들어간다.
    /// 콘텐츠 업데이트 빌드도 로컬 카탈로그를 새로 쓰므로, 출시본을 흉내 내는 플레이어는 전체 빌드 직후 업데이트 전에 만든다.
    /// </summary>
    public static class PlayerBuildMenu
    {
        public const string OutputRoot = "Builds";

        [MenuItem("Lobby Flow/플레이어 빌드/Windows 개발 빌드")]
        private static void BuildWindowsFromMenu()
        {
            if (BuildWindowsDevelopment(out var path))
                EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Lobby Flow/플레이어 빌드/Android 개발 빌드")]
        private static void BuildAndroidFromMenu()
        {
            if (BuildAndroidDevelopment(false, out var path))
                EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Lobby Flow/플레이어 빌드/Android 개발 빌드 후 기기 실행")]
        private static void BuildAndRunAndroidFromMenu() => BuildAndroidDevelopment(true, out _);

        /// <returns>성공하면 true.</returns>
        public static bool BuildWindowsDevelopment(out string path)
        {
            path = $"{OutputRoot}/Windows/LobbyFlow.exe";
            return Build(BuildTarget.StandaloneWindows64, path, BuildOptions.Development);
        }

        /// <param name="runOnDevice">USB 로 연결된 기기에 설치하고 실행한다. 실행 전에 기기의 localhost:8080 을 PC 콘텐츠 서버로 잇는다.</param>
        /// <returns>성공하면 true.</returns>
        public static bool BuildAndroidDevelopment(bool runOnDevice, out string path)
        {
            path = $"{OutputRoot}/Android/LobbyFlow.apk";

            // 개발 빌드는 기기에 바로 설치하는 APK 로 만든다. 스토어용 AAB 는 출시 빌드에서 다룬다.
            EditorUserBuildSettings.buildAppBundle = false;

            var options = BuildOptions.Development;
            if (runOnDevice)
            {
                // LocalServer 프로필은 localhost 다. 앱이 켜지자마자 카탈로그를 확인하므로 설치 전에 연결해 둔다.
                if (!ContentServer.ReverseToAndroidDevice())
                    return false;

                options |= BuildOptions.AutoRunPlayer;
            }

            return Build(BuildTarget.Android, path, options);
        }

        private static bool Build(BuildTarget target, string path, BuildOptions options)
        {
            // 플랫폼 전환은 전체 재임포트를 부르므로 메뉴에서 하지 않는다. Jenkins 는 -buildTarget 으로 연다.
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.LogError($"[LobbyFlow] 현재 플랫폼({EditorUserBuildSettings.activeBuildTarget})이 {target} 이 아니다. 플랫폼을 바꾼 뒤 빌드한다.");
                return false;
            }

            // 'Build Addressables on Player Build' 를 끄고 쓰므로, 빌드된 콘텐츠가 없으면 빈 플레이어가 나온다.
            var runtimeSettingsPath = Path.Combine(Addressables.BuildPath, "settings.json");
            if (!File.Exists(runtimeSettingsPath))
            {
                Debug.LogError($"[LobbyFlow] Addressables 빌드 결과가 없다: {Addressables.BuildPath}. 전체 빌드를 먼저 한다.");
                return false;
            }

            // 프로필을 바꾸거나 다른 플랫폼에서 빌드한 뒤 이 플랫폼을 다시 빌드하지 않으면, 이전 원격 주소(예: localhost)가 플레이어에 들어간다.
            var aaSettings = AddressableAssetSettingsDefaultObject.Settings;
            var remoteCatalog = aaSettings != null ? aaSettings.RemoteCatalogLoadPath.GetValue(aaSettings) : null;
            if (!string.IsNullOrEmpty(remoteCatalog) && !File.ReadAllText(runtimeSettingsPath).Contains(remoteCatalog))
            {
                Debug.LogError($"[LobbyFlow] {target} Addressables 빌드가 현재 프로필({AddressablesBuildMenu.ActiveProfileName})의 원격 주소와 다르다. 이 플랫폼에서 전체 빌드를 다시 한다.");
                return false;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = target,
                options = options,
            });
            RestoreProjectFilesAfterBuild();

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[LobbyFlow] 플레이어 빌드 실패({summary.result}): 오류 {summary.totalErrors}개");
                return false;
            }

            Debug.Log($"[LobbyFlow] 플레이어 빌드 완료 ({summary.totalTime.TotalSeconds:0.0}초, {summary.totalSize / (1024f * 1024f):0.0}MB): {Path.GetFullPath(path)}");
            return true;
        }

        /// <summary>
        /// 빌드 동안만 바뀌는 프로젝트 파일을 디스크에서도 빌드 전 상태로 돌린다. 두 패키지 모두 빌드 뒤 메모리만 되돌려,
        /// 그대로 두면 다음 저장이나 스크립트 다시 읽기 전까지 빌드 도중 상태가 git 변경으로 보인다.
        /// </summary>
        private static void RestoreProjectFilesAfterBuild()
        {
            // Input System 은 빌드 동안 액션 에셋을 preloadedAssets 에 넣었다가 빌드 뒤 메모리에서만 뺀다.
            EditorUtility.SetDirty(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));

            // Addressables 는 코드 제거 방지 목록(link.xml)을 설정 폴더에 복사해 두고 다음 스크립트 다시 읽기 때 지운다.
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                var linkPath = $"{settings.ConfigFolder}/link.xml";
                if (AssetDatabase.LoadMainAssetAtPath(linkPath) != null)
                    AssetDatabase.DeleteAsset(linkPath);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
