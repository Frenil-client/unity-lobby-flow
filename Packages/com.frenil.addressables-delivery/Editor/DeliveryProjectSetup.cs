using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;

namespace AddressablesDelivery.Editor
{
    /// <summary>
    /// 원격 콘텐츠 배포에 필요한 프로젝트 설정을 한 번에 맞춘다. 여러 번 실행해도 결과가 같고, 그룹과 항목은 건드리지 않는다.
    /// <list type="bullet">
    /// <item>설정 에셋(<see cref="DeliverySettings"/>)이 없으면 <c>Assets/Resources</c> 에 만든다.</item>
    /// <item>LocalServer 프로필(개발 PC 콘텐츠 서버)과 원격 프로필(CCD 또는 Custom 주소).</item>
    /// <item>원격 카탈로그를 만들고, 시작할 때 자동으로 갱신하지 않는다(패치 서비스가 직접 확인한다).</item>
    /// <item>JSON 카탈로그와 그 스크립트 심볼(Standalone, Android, iOS).</item>
    /// <item>공통 번들 이름 고정(빌드하는 폴더가 달라도 번들 해시가 같게).</item>
    /// <item>플레이어 빌드가 Addressables 를 빌드하지 않게(업데이트 기준이 몰래 바뀌지 않게).</item>
    /// <item>http(LocalServer)는 개발 빌드에서만 허용.</item>
    /// </list>
    /// </summary>
    public static class DeliveryProjectSetup
    {
        public const string MenuPath = "Tools/Addressables Delivery/프로젝트에 적용";
        public const string ServerHostVariable = "ServerHost";
        public const string DefaultSettingsPath = "Assets/Resources/" + DeliverySettings.ResourceName + ".asset";

        private const string JsonCatalogSymbol = "ENABLE_JSON_CATALOG";
        private const string LogPrefix = "[Delivery]";

        private static readonly NamedBuildTarget[] SymbolTargets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
        };

        [MenuItem(MenuPath, priority = 1)]
        private static void ApplyFromMenu()
        {
            if (Apply())
                EditorGUIUtility.PingObject(DeliverySettings.Current);
        }

        [MenuItem("Tools/Addressables Delivery/설정 에셋 선택", priority = 2)]
        private static void SelectSettings()
        {
            if (DeliverySettings.Current == null)
            {
                Debug.LogWarning($"{LogPrefix} 설정 에셋이 없다. {MenuPath} 을 실행한다.");
                return;
            }

            Selection.activeObject = DeliverySettings.Current;
        }

        /// <returns>문제 없이 적용했으면 true. 경고는 콘솔에 남긴다.</returns>
        public static bool Apply()
        {
            var delivery = EnsureSettingsAsset();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var ok = CheckSettingsAsset(delivery);

            ApplyProfiles(settings, delivery);

            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            settings.DisableCatalogUpdateOnStartup = true;
            settings.EnableJsonCatalog = true;

            settings.BuiltInBundleNaming = BuiltInBundleNaming.Custom;
            settings.BuiltInBundleCustomNaming = delivery.SharedBundlePrefix;
            settings.MonoScriptBundleNaming = MonoScriptBundleNaming.Custom;
            settings.MonoScriptBundleCustomNaming = delivery.SharedBundlePrefix;

            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            EditorUtility.SetDirty(settings);

            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            var symbolsChanged = EnsureJsonCatalogSymbol();
            // PlayerSettings API 로 바꾼 값은 설정 에셋이 변경됨으로 표시되지 않아 Save Project 에도 파일에 남지 않을 수 있다.
            EditorUtility.SetDirty(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            AssetDatabase.SaveAssets();

            if (symbolsChanged)
                Debug.LogWarning($"{LogPrefix} {JsonCatalogSymbol} 심볼을 추가했다. 스크립트를 다시 컴파일한 뒤 Addressables 빌드와 Play 를 한다.");

            Debug.Log($"{LogPrefix} 프로젝트 설정 적용 완료: 저장소 {delivery.Storage}, 원격 프로필 {AddressablesBuild.RemoteProfile}, " +
                      $"공통 번들 접두어 '{delivery.SharedBundlePrefix}', 활성 프로필 {AddressablesBuild.ActiveProfileName}");
            return ok;
        }

        /// <summary>원격 프로필에 들어갈 받는 주소. CCD 는 실행 중에 설정 에셋 값을 읽는 주소다.</summary>
        public static string RemoteLoadUrl(DeliverySettings delivery) =>
            delivery.Storage == RemoteStorage.Custom ? delivery.CustomLoadUrl : CcdLocation.LoadUrlTemplate(delivery.CcdUrlClass);

        /// <summary>LocalServer 프로필의 받는 주소.</summary>
        public static string LocalServerLoadUrl(DeliverySettings delivery) =>
            $"http://[{ServerHostVariable}]:{delivery.LocalServerPort}/[BuildTarget]";

        private static void ApplyProfiles(AddressableAssetSettings settings, DeliverySettings delivery)
        {
            var profiles = settings.profileSettings;
            if (!profiles.GetVariableNames().Contains(ServerHostVariable))
                profiles.CreateValue(ServerHostVariable, "localhost");

            var localId = profiles.GetProfileId(AddressablesBuild.LocalServerProfile);
            if (string.IsNullOrEmpty(localId))
                localId = profiles.AddProfile(AddressablesBuild.LocalServerProfile, settings.activeProfileId);

            profiles.SetValue(localId, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kRemoteBuildPathValue);
            profiles.SetValue(localId, AddressableAssetSettings.kRemoteLoadPath, LocalServerLoadUrl(delivery));

            var remoteName = AddressablesBuild.RemoteProfile;
            var remoteId = profiles.GetProfileId(remoteName);
            if (string.IsNullOrEmpty(remoteId))
                remoteId = profiles.AddProfile(remoteName, localId);

            profiles.SetValue(remoteId, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kRemoteBuildPathValue);
            profiles.SetValue(remoteId, AddressableAssetSettings.kRemoteLoadPath, RemoteLoadUrl(delivery));

            // 처음 적용할 때만 LocalServer 를 고른다. 다시 적용해도 사용자가 고른 프로필은 그대로 둔다.
            if (settings.activeProfileId != localId && settings.activeProfileId != remoteId)
                settings.activeProfileId = localId;
        }

        private static DeliverySettings EnsureSettingsAsset()
        {
            var existing = DeliverySettings.Current;
            if (existing != null)
                return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(DefaultSettingsPath));
            var created = ScriptableObject.CreateInstance<DeliverySettings>();
            var serialized = new SerializedObject(created);
            var prefix = Regex.Replace(PlayerSettings.productName.ToLowerInvariant(), "[^a-z0-9]", string.Empty);
            serialized.FindProperty("_sharedBundlePrefix").stringValue = string.IsNullOrEmpty(prefix) ? "content" : prefix;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(created, DefaultSettingsPath);
            AssetDatabase.SaveAssets();
            DeliverySettings.SetCurrent(created);
            Debug.Log($"{LogPrefix} 설정 에셋을 만들었다: {DefaultSettingsPath}. 저장소 정보(CCD 프로젝트, 버킷 ID 등)를 채운다.");
            return created;
        }

        /// <summary>플레이어가 실행 중에 값을 읽을 수 있는 상태인지 본다. 문제마다 경고를 남긴다.</summary>
        private static bool CheckSettingsAsset(DeliverySettings delivery)
        {
            var problems = new List<string>();
            var path = AssetDatabase.GetAssetPath(delivery);
            if (!path.Contains("/Resources/") || Path.GetFileNameWithoutExtension(path) != DeliverySettings.ResourceName)
                problems.Add($"설정 에셋이 Resources 폴더의 {DeliverySettings.ResourceName}.asset 이 아니다({path}). 플레이어가 찾지 못한다.");

            if (string.IsNullOrEmpty(delivery.SharedBundlePrefix))
                problems.Add("공통 번들 접두어가 비어 있다. 빌드하는 폴더마다 번들 해시가 달라진다.");

            if (delivery.Storage == RemoteStorage.Ccd)
            {
                if (string.IsNullOrEmpty(delivery.CcdProjectId))
                    problems.Add("CCD 프로젝트 ID 가 비어 있다.");

                foreach (var (platform, bucket) in new[] { ("Windows", delivery.WindowsBucket), ("Android", delivery.AndroidBucket), ("iOS", delivery.IosBucket) })
                {
                    if (string.IsNullOrEmpty(bucket.Name) != string.IsNullOrEmpty(bucket.Id))
                        problems.Add($"{platform} 버킷의 이름과 ID 중 하나만 있다.");
                }

                if (!HasUrlProperties(delivery.CcdUrlClass, out var missing))
                    problems.Add($"CCD 주소 클래스 '{delivery.CcdUrlClass}' 에 공개 정적 속성 {missing} 이(가) 없다. 실행 중 주소를 만들 수 없다.");
            }
            else if (!delivery.CustomLoadUrl.StartsWith("http"))
            {
                problems.Add($"받는 주소가 http(s) 가 아니다: {delivery.CustomLoadUrl}");
            }

            foreach (var problem in problems)
                Debug.LogWarning($"{LogPrefix} {problem}");

            return problems.Count == 0;
        }

        /// <summary>Addressables 가 런타임 변수 <c>{클래스.속성}</c> 을 찾는 방식(공개 정적 속성 또는 필드)으로 네 값이 있는지 본다.</summary>
        public static bool HasUrlProperties(string className, out string missing)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(className, false, false))
                .FirstOrDefault(t => t != null);

            var names = new[] { "ProjectId", "Environment", "BucketId", "Badge" };
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
            var absent = names.Where(n => type == null || (type.GetProperty(n, flags) == null && type.GetField(n, flags) == null)).ToArray();
            missing = string.Join(", ", absent);
            return absent.Length == 0;
        }

        private static bool EnsureJsonCatalogSymbol()
        {
            // JSON 카탈로그는 설정 값만으로는 켜지지 않고 ENABLE_JSON_CATALOG 심볼로 빌드·런타임 코드가 갈린다. 설정 창의 토글과 같게 넣는다.
            var changed = false;
            foreach (var target in SymbolTargets)
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out var symbols);
                if (Array.IndexOf(symbols, JsonCatalogSymbol) >= 0)
                    continue;

                PlayerSettings.SetScriptingDefineSymbols(target, symbols.Append(JsonCatalogSymbol).ToArray());
                changed = true;
            }

            return changed;
        }
    }
}
