using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// Addressables 전체 빌드와 콘텐츠 업데이트 빌드(SPEC 5.7). 메뉴와 Jenkins 배치 실행이 같은 메서드를 쓴다.
    /// 전체 빌드가 만든 <c>addressables_content_state.bin</c> 이 다음 콘텐츠 업데이트의 기준이므로 커밋한다.
    /// </summary>
    public static class AddressablesBuildMenu
    {
        [MenuItem("Lobby Flow/Addressables/전체 빌드 (기준 갱신)")]
        private static void BuildFullFromMenu()
        {
            if (EditorUtility.DisplayDialog("Addressables 전체 빌드",
                    "콘텐츠 업데이트 기준(addressables_content_state.bin)이 새로 만들어지고, 원격 빌드 폴더의 이전 번들은 지워집니다. 이미 배포한 플레이어가 있다면 그 기준을 덮어씁니다.",
                    "빌드", "취소"))
                BuildFull();
        }

        [MenuItem("Lobby Flow/Addressables/콘텐츠 업데이트 빌드")]
        private static void BuildUpdateFromMenu() => BuildContentUpdate();

        /// <returns>성공하면 true.</returns>
        public static bool BuildFull()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!CanBuild(settings))
                return false;

            // 전체 빌드는 새 기준이다. 이전 빌드의 번들이 남아 있으면 서버 로그에서 받은 번들을 구분하기 어렵다.
            var remoteBuildPath = settings.RemoteCatalogBuildPath.GetValue(settings);
            if (Directory.Exists(remoteBuildPath))
                Directory.Delete(remoteBuildPath, true);

            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"[LobbyFlow] Addressables 전체 빌드 실패: {result.Error}");
                return false;
            }

            Debug.Log($"[LobbyFlow] Addressables 전체 빌드 완료 ({result.Duration:0.0}초). 기준: {ContentUpdateScript.GetContentStateDataPath(false, settings)}");
            return true;
        }

        /// <returns>성공하면 true.</returns>
        public static bool BuildContentUpdate()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!CanBuild(settings))
                return false;

            var statePath = ContentUpdateScript.GetContentStateDataPath(false, settings);
            if (!File.Exists(statePath))
            {
                Debug.LogError($"[LobbyFlow] 콘텐츠 업데이트 기준이 없다: {statePath}. 전체 빌드를 먼저 한다.");
                return false;
            }

            // Addressables 2.8.1 은 업데이트 빌드에서 빌드 리포트(Debug Build Layout)를 만들다 로컬 씬 번들을 두 번 넣어
            // 예외를 낸다. 번들과 카탈로그 결과와는 무관한 리포트 단계라 업데이트 빌드 동안만 끈다.
            var generateLayout = ProjectConfigData.GenerateBuildLayout;
            ProjectConfigData.GenerateBuildLayout = false;
            AddressablesPlayerBuildResult result;
            try
            {
                result = ContentUpdateScript.BuildContentUpdate(settings, statePath);
            }
            finally
            {
                ProjectConfigData.GenerateBuildLayout = generateLayout;
            }

            if (result == null || !string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"[LobbyFlow] 콘텐츠 업데이트 빌드 실패: {result?.Error}");
                return false;
            }

            Debug.Log($"[LobbyFlow] 콘텐츠 업데이트 빌드 완료 ({result.Duration:0.0}초). 기준: {statePath}");
            return true;
        }

        private static bool CanBuild(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("[LobbyFlow] Addressables 설정이 없다. 빌더의 그룹 구성을 먼저 실행한다.");
                return false;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("[LobbyFlow] 스크립트 컴파일 중이다. 컴파일이 끝난 뒤 다시 빌드한다.");
                return false;
            }

            // JSON 카탈로그는 ENABLE_JSON_CATALOG 심볼로 빌드 코드가 갈린다. 심볼을 넣고 아직 다시 읽지 않은 상태로
            // 빌드하면 설정과 다른 형식(바이너리)의 카탈로그가 나온다(SPEC 6.2).
#if ENABLE_JSON_CATALOG
            const bool jsonCompiled = true;
#else
            const bool jsonCompiled = false;
#endif
            if (settings.EnableJsonCatalog != jsonCompiled)
            {
                Debug.LogError($"[LobbyFlow] JSON 카탈로그 설정({settings.EnableJsonCatalog})과 컴파일된 심볼({jsonCompiled})이 다르다. " +
                               "스크립트를 다시 컴파일한 뒤 빌드한다.");
                return false;
            }

            return true;
        }
    }
}
