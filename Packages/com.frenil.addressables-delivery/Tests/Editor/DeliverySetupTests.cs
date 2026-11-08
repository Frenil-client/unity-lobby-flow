using System.Collections.Generic;
using System.IO;
using AddressablesDelivery.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace AddressablesDelivery.Tests
{
    /// <summary>
    /// 프로젝트 설정과 그룹 규칙 검사. 프로젝트에 적용 메뉴가 맞춘 설정이 그대로인지, 그룹 구성이 콘텐츠 업데이트에서 문제를 내지 않는지 본다.
    /// 프로젝트 manifest 의 <c>testables</c> 에 패키지 이름을 넣으면 Test Runner 와 CI 가 함께 돌린다.
    /// </summary>
    public sealed class DeliverySetupTests
    {
        private AddressableAssetSettings _settings;
        private DeliverySettings _delivery;

        [OneTimeSetUp]
        public void LoadSettings()
        {
            _settings = AddressableAssetSettingsDefaultObject.Settings;
            _delivery = DeliverySettings.Current;
            Assert.That(_settings, Is.Not.Null, "Addressables 설정이 없다");
            Assert.That(_delivery, Is.Not.Null, $"{nameof(DeliverySettings)} 가 없다. {DeliveryProjectSetup.MenuPath} 을 실행한다");
        }

        [Test]
        public void SettingsAssetIsInResources()
        {
            // 플레이어는 Resources 에서 이름으로 찾는다. 다른 곳에 두면 에디터에서는 되고 빌드에서만 원격 주소가 비게 된다.
            var path = AssetDatabase.GetAssetPath(_delivery);
            Assert.That(path, Does.Contain("/Resources/"));
            Assert.That(Path.GetFileNameWithoutExtension(path), Is.EqualTo(DeliverySettings.ResourceName));
        }

        [Test]
        public void RemoteCatalogIsCheckedOnlyByPatchService()
        {
            // 패치 서비스가 시작 화면에서 카탈로그를 확인하고 갱신한다. 자동 갱신을 켜 두면 확인 전에 이미 바뀌어 받을 크기와 로그가 어긋난다.
            Assert.That(_settings.BuildRemoteCatalog, Is.True);
            Assert.That(_settings.DisableCatalogUpdateOnStartup, Is.True);
            Assert.That(_settings.RemoteCatalogLoadPath.GetName(_settings), Is.EqualTo(AddressableAssetSettings.kRemoteLoadPath));
        }

        [Test]
        public void JsonCatalogSettingMatchesCompiledSymbol()
        {
            // 설정만 켜고 심볼이 없으면 바이너리 카탈로그가 나온다. 심볼은 프로젝트에 적용이 넣고 컴파일 뒤에 반영된다.
#if ENABLE_JSON_CATALOG
            const bool compiled = true;
#else
            const bool compiled = false;
#endif
            Assert.That(_settings.EnableJsonCatalog, Is.True);
            Assert.That(compiled, Is.True, "ENABLE_JSON_CATALOG 심볼이 컴파일되지 않았다");
        }

        [Test]
        public void PlayerBuildDoesNotBuildAddressables()
        {
            // 플레이어 빌드가 전체 빌드를 하면 업데이트 기준이 몰래 바뀐다.
            Assert.That(_settings.BuildAddressablesWithPlayerBuild, Is.EqualTo(AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer));
        }

        [Test]
        public void SharedBundleNamesDoNotDependOnProjectFolder()
        {
            // 기본값(프로젝트 폴더 이름 해시)이면 CI 작업 폴더와 로컬 폴더에서 번들 해시가 달라진다.
            Assert.That(_delivery.SharedBundlePrefix, Is.Not.Empty);
            Assert.That(_settings.BuiltInBundleNaming, Is.EqualTo(BuiltInBundleNaming.Custom));
            Assert.That(_settings.BuiltInBundleCustomNaming, Is.EqualTo(_delivery.SharedBundlePrefix));
            Assert.That(_settings.MonoScriptBundleNaming, Is.EqualTo(MonoScriptBundleNaming.Custom));
            Assert.That(_settings.MonoScriptBundleCustomNaming, Is.EqualTo(_delivery.SharedBundlePrefix));
        }

        [Test]
        public void ProfilesPointToConfiguredLocations()
        {
            var profiles = _settings.profileSettings;
            var local = profiles.GetProfileId(AddressablesBuild.LocalServerProfile);
            var remote = profiles.GetProfileId(AddressablesBuild.RemoteProfile);
            Assert.That(local, Is.Not.Empty, $"{AddressablesBuild.LocalServerProfile} 프로필이 없다");
            Assert.That(remote, Is.Not.Empty, $"{AddressablesBuild.RemoteProfile} 프로필이 없다");

            Assert.That(profiles.GetValueByName(local, AddressableAssetSettings.kRemoteLoadPath), Is.EqualTo(DeliveryProjectSetup.LocalServerLoadUrl(_delivery)));
            Assert.That(profiles.GetValueByName(remote, AddressableAssetSettings.kRemoteLoadPath), Is.EqualTo(DeliveryProjectSetup.RemoteLoadUrl(_delivery)));
        }

        [Test]
        public void CcdLocationIsComplete()
        {
            if (_delivery.Storage != RemoteStorage.Ccd)
                Assert.Ignore("CCD 를 쓰지 않는다");

            // 프로젝트에 적용 직후(저장소 정보를 채우기 전)는 건너뛴다. 일부만 채웠으면 실패한다.
            var buckets = new[] { _delivery.WindowsBucket, _delivery.AndroidBucket, _delivery.IosBucket };
            var hasBucket = System.Array.Exists(buckets, b => b.Id.Length > 0 || b.Name.Length > 0);
            if (string.IsNullOrEmpty(_delivery.CcdProjectId) && !hasBucket)
                Assert.Ignore("CCD 정보가 아직 비어 있다. 설정 에셋에 프로젝트 ID 와 버킷을 채우면 검사한다");

            Assert.That(_delivery.CcdProjectId, Is.Not.Empty, "CCD 프로젝트 ID 가 비어 있다");
            Assert.That(DeliveryProjectSetup.HasUrlProperties(_delivery.CcdUrlClass, out var missing), Is.True,
                $"{_delivery.CcdUrlClass} 에 {missing} 이(가) 없다");

            Assert.That(System.Array.Exists(buckets, b => b.Id.Length > 0), Is.True, "버킷 ID 가 하나도 없다");
            foreach (var bucket in buckets)
                Assert.That(bucket.Name.Length > 0, Is.EqualTo(bucket.Id.Length > 0), $"버킷 이름과 ID 중 하나만 있다: {bucket.Name}/{bucket.Id}");

            var url = CcdLocation.EntryUrl(CcdLocation.Current, CcdLocation.ReleaseBadge, "catalog.hash");
            Assert.That(url, Does.Contain(_delivery.CcdProjectId).And.Contain($"/release_by_badge/{CcdLocation.ReleaseBadge}/").And.EndWith("?path=/catalog.hash"));
        }

        [Test]
        public void BadgeTransformReplacesOnlyCcdBadge()
        {
            // 개발 빌드는 카탈로그에 든 주소의 배지를 요청할 때 지금 배지로 바꾼다. 로컬 파일 경로와 배지가 없는 주소는 그대로 둔다.
            var bucket = CcdLocation.Current;
            var other = CcdLocation.Badge + "-other";
            Assert.That(CcdLocation.ReplaceBadge(CcdLocation.EntryUrl(bucket, other, "catalog.hash")),
                Is.EqualTo(CcdLocation.EntryUrl(bucket, CcdLocation.Badge, "catalog.hash")));

            var current = CcdLocation.EntryUrl(bucket, CcdLocation.Badge, "a.bundle");
            Assert.That(CcdLocation.ReplaceBadge(current), Is.SameAs(current));

            const string local = "C:/Data/release_by_badge/v1/a.bundle";
            Assert.That(CcdLocation.ReplaceBadge(local), Is.SameAs(local));
            Assert.That(CcdLocation.ReplaceBadge("https://example.com/content/a.bundle"), Is.EqualTo("https://example.com/content/a.bundle"));
        }

        [Test]
        public void LocalGroupsAreStaticAndRemoteGroupsAreNot()
        {
            // 로컬 그룹은 앱과 함께 나가 바꿀 수 없고(정적), 원격 그룹은 업데이트로 바꾼다. 섞이면 업데이트 빌드가 엉뚱한 그룹으로 옮긴다.
            foreach (var group in _settings.groups)
            {
                if (group == null || group.entries.Count == 0)
                    continue;

                var update = group.GetSchema<ContentUpdateGroupSchema>();
                Assert.That(update, Is.Not.Null, $"{group.Name} 에 콘텐츠 업데이트 설정이 없다");
                Assert.That(update.StaticContent, Is.EqualTo(!IsRemote(group)), $"{group.Name} 의 정적 콘텐츠 설정이 원격 여부와 맞지 않다");
            }
        }

        [Test]
        public void FolderEntriesDoNotMixScenesWithOtherAssets()
        {
            // Addressables 2.8.1 은 폴더 항목의 번들 ID 를 첫 하위 항목 기준으로 정한다. 씬과 다른 에셋이 섞이면 기준 파일에 다른 에셋도
            // 씬 번들로 기록되고, 콘텐츠 업데이트 빌드가 바뀌지 않은 정적 그룹의 assets 번들 위치를 씬 번들로 바꿔 쓴다. 씬은 파일 항목으로 둔다.
            var mixed = new List<string>();
            foreach (var group in _settings.groups)
            {
                if (group == null)
                    continue;

                foreach (var entry in group.entries)
                {
                    // IsFolder 는 저장되지 않고 Groups 창 등이 항목을 훑을 때 채워진다. 새로 연 프로젝트(CI)에서는 비어 있어 에셋 데이터베이스로 본다.
                    if (!AssetDatabase.IsValidFolder(entry.AssetPath))
                        continue;

                    var hasScene = false;
                    var hasOther = false;
                    foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { entry.AssetPath }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (AssetDatabase.IsValidFolder(path))
                            continue;

                        if (path.EndsWith(".unity"))
                            hasScene = true;
                        else
                            hasOther = true;
                    }

                    if (hasScene && hasOther)
                        mixed.Add($"{group.Name}: {entry.AssetPath}");
                }
            }

            Assert.That(mixed, Is.Empty, "씬과 다른 에셋이 한 폴더 항목에 있다");
        }

        private bool IsRemote(AddressableAssetGroup group)
        {
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            return bundled != null && bundled.LoadPath.GetName(_settings) == AddressableAssetSettings.kRemoteLoadPath;
        }
    }
}
