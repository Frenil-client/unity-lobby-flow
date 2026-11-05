using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// 플레이어 빌드(SPEC 5.7). Addressables 콘텐츠는 빌드 창에서 따로 만들고, 플레이어에는 그때의 로컬 콘텐츠와 카탈로그가 들어간다.
    /// 콘텐츠 업데이트 빌드도 로컬 카탈로그를 새로 쓰므로, 출시본을 흉내 내는 플레이어는 전체 빌드 직후 업데이트 전에 만든다.
    /// </summary>
    public static class PlayerBuild
    {
        public const string OutputRoot = "Builds";

        /// <returns>성공하면 true.</returns>
        public static bool BuildWindowsDevelopment(out string path)
        {
            path = $"{OutputRoot}/Windows/LobbyFlow.exe";
            return Build(BuildTarget.StandaloneWindows64, path, BuildOptions.Development);
        }

        /// <param name="runOnDevice">USB 로 연결된 기기에 설치하고 실행한다. LocalServer 프로필이면 실행 전에 기기의 localhost:8080 을 PC 콘텐츠 서버로 잇는다.</param>
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
                if (AddressablesBuild.ActiveProfileName == AddressablesBuild.LocalServerProfile && !ContentServer.ReverseToAndroidDevice())
                    return false;

                options |= BuildOptions.AutoRunPlayer;
            }

            return Build(BuildTarget.Android, path, options);
        }

        private static bool Build(BuildTarget target, string path, BuildOptions options)
        {
            // 플랫폼 전환은 전체 재임포트를 부르므로 빌드 창에서 하지 않는다. Jenkins 는 -buildTarget 으로 연다.
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                Debug.LogError($"[LobbyFlow] 현재 플랫폼({EditorUserBuildSettings.activeBuildTarget})이 {target} 이 아니다. 플랫폼을 바꾼 뒤 빌드한다.");
                return false;
            }

            // 'Build Addressables on Player Build' 를 끄고 쓰므로, 이 플랫폼의 Addressables 빌드가 지금 프로필로 되어 있어야 한다.
            if (!AddressablesBuild.IsBuildCurrent(out var buildState))
            {
                Debug.LogError($"[LobbyFlow] {target} Addressables 빌드: {buildState}");
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
