using System.IO;
using LobbyFlow.Patching;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace LobbyFlow.Editor
{
    /// <summary>
    /// Addressables 프로필 전환, 전체 빌드, 콘텐츠 업데이트 빌드(SPEC 5.7). 빌드 창(<see cref="BuildWindow"/>)과 Jenkins 배치 실행이 같은 메서드를 쓴다.
    /// 전체 빌드가 만든 <c>addressables_content_state.bin</c> 이 다음 콘텐츠 업데이트의 기준이므로 커밋한다.
    /// </summary>
    public static class AddressablesBuild
    {
        public const string LocalServerProfile = "LocalServer";
        public const string CcdProfile = "CCD";

        /// <summary>
        /// 원격 주소 프로필을 바꾼다(SPEC 5.4). 주소는 카탈로그에 들어가므로 바꾼 뒤에는 전체 빌드와 플레이어 빌드를 다시 한다.
        /// </summary>
        /// <returns>프로필이 있으면 true.</returns>
        public static bool UseProfile(string profileName)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var profileId = settings != null ? settings.profileSettings.GetProfileId(profileName) : null;
            if (string.IsNullOrEmpty(profileId))
            {
                Debug.LogError($"[LobbyFlow] Addressables 프로필 '{profileName}' 이 없다. 빌더의 그룹 구성을 먼저 실행한다.");
                return false;
            }

            if (settings.activeProfileId == profileId)
                return true;

            settings.activeProfileId = profileId;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LobbyFlow] Addressables 프로필을 {profileName} 로 바꿨다. 전체 빌드와 플레이어 빌드를 다시 한다.");
            return true;
        }

        public static string ActiveProfileName
        {
            get
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                return settings != null ? settings.profileSettings.GetProfileName(settings.activeProfileId) : null;
            }
        }

        /// <summary>
        /// 현재 플랫폼의 Addressables 빌드 결과(<c>Library/com.unity.addressables/aa</c>)가 지금 프로필로 만든 것인지 본다.
        /// 프로필을 바꾸거나 다른 플랫폼에서 빌드한 뒤 이 플랫폼을 다시 빌드하지 않으면 이전 원격 주소가 플레이어에 들어간다.
        /// </summary>
        /// <param name="message">상태 설명. 빌드 창과 플레이어 빌드 거부 로그에 쓴다.</param>
        /// <returns>플레이어 빌드에 써도 되면 true.</returns>
        public static bool IsBuildCurrent(out string message)
        {
            var runtimeSettingsPath = Path.Combine(Addressables.BuildPath, "settings.json");
            if (!File.Exists(runtimeSettingsPath))
            {
                message = "빌드 결과 없음. 전체 빌드를 한다.";
                return false;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var remoteCatalog = settings != null ? settings.RemoteCatalogLoadPath.GetValue(settings) : null;
            if (!string.IsNullOrEmpty(remoteCatalog) && !File.ReadAllText(runtimeSettingsPath).Contains(remoteCatalog))
            {
                message = $"현재 프로필({ActiveProfileName})과 원격 주소가 다르다. 전체 빌드를 다시 한다.";
                return false;
            }

            message = $"현재 프로필({ActiveProfileName})로 빌드됨 ({File.GetLastWriteTime(runtimeSettingsPath):MM-dd HH:mm})";
            return true;
        }

        /// <returns>성공하면 true.</returns>
        public static bool BuildFull()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!CanBuild(settings))
                return false;

            ClearDynamicFontData();

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

            ClearDynamicFontData();

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

        /// <summary>
        /// 동적 TMP 폰트 에셋의 글자와 아틀라스를 비운다. 에디터에서 플레이하면 쓴 글자가 아틀라스에 쌓여 에셋이 바뀌는데,
        /// 그대로 빌드하면 내용이 같아도 로컬 폰트 번들 해시가 달라져 콘텐츠 업데이트가 정적 그룹 변경으로 본다.
        /// 플레이어 빌드는 TMP 의 Clear Dynamic Data On Build 가 같은 일을 한다.
        /// </summary>
        public static void ClearDynamicFontData()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/LobbyFlowContent" }))
            {
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (fontAsset == null || fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
                    continue;

                fontAsset.ClearFontAssetData(true);
                EditorUtility.SetDirty(fontAsset);
                foreach (var atlas in fontAsset.atlasTextures)
                {
                    if (atlas != null)
                        EditorUtility.SetDirty(atlas);
                }
            }

            AssetDatabase.SaveAssets();
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

            // CCD 주소의 프로젝트·버킷 ID 는 실행 중에 채워지므로, 비어 있으면 빌드는 되지만 기기에서 받을 수 없다.
            if (ActiveProfileName == CcdProfile && (string.IsNullOrEmpty(CcdConfig.ProjectId) || string.IsNullOrEmpty(CcdConfig.Current.Id)))
            {
                Debug.LogError($"[LobbyFlow] CCD 프로필인데 CcdConfig 의 프로젝트 ID 또는 {CcdConfig.Current.Name} 버킷 ID 가 비어 있다.");
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
