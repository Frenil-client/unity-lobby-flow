using System.Collections.Generic;
using System.IO;
using LobbyFlow.Content;
using LobbyFlow.Patching;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace LobbyFlow.Tests
{
    /// <summary>
    /// 커밋된 콘텐츠와 Addressables 설정의 무결성(SPEC 5.2, 5.3, 5.6). 데이터 파일이 가리키는 주소가 실제 파일로 이어지는지,
    /// 다운로드 라벨이 원격 그룹에 붙어 있는지, 로컬·원격 그룹의 업데이트 규칙이 맞는지 본다. Jenkins 가 빌드 전에 돌린다.
    /// </summary>
    public sealed class ContentIntegrityTests
    {
        private AddressableAssetSettings _settings;
        private ContentTables _content;

        [OneTimeSetUp]
        public void LoadSettingsAndData()
        {
            _settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(_settings, Is.Not.Null, "Addressables 설정이 없다");

            _content = ContentTables.Parse(
                ReadAddress(ContentAddresses.CharactersCsv),
                ReadAddress(ContentAddresses.ChaptersCsv),
                ReadAddress(ContentAddresses.CostumesCsv),
                ReadAddress(ContentAddresses.EventNotice));
        }

        [Test]
        public void EveryAddressInDataPointsToAFile()
        {
            var missing = new List<string>();
            void Check(string owner, string address)
            {
                var path = ResolvePath(address);
                if (path == null || !File.Exists(path))
                    missing.Add($"{owner}: {address}");
            }

            foreach (var character in _content.Characters)
            {
                Check(character.Id, character.PortraitAddress);
                Check(character.Id, character.PrefabAddress);
            }

            foreach (var chapter in _content.Chapters)
            {
                Check(chapter.Id, chapter.SceneAddress);
                Check(chapter.Id, chapter.IconAddress);
            }

            foreach (var costume in _content.Costumes)
                Check(costume.Id, costume.PrefabAddress);

            Check(nameof(ContentAddresses), ContentAddresses.EventBanner);
            Check(nameof(ContentAddresses), ContentAddresses.LobbyBgm);

            Assert.That(missing, Is.Empty, "주소에 해당하는 파일이 없다");
        }

        [Test]
        public void DownloadLabelsAreOnRemoteEntriesThatHoldTheirContent()
        {
            var labels = _settings.GetLabels();
            foreach (var chapter in _content.Chapters)
            {
                var entry = FindEntry(chapter.SceneAddress);
                Assert.That(entry, Is.Not.Null, $"{chapter.Id} 씬 주소가 그룹에 없다");

                if (!chapter.RequiresDownload)
                {
                    Assert.That(IsRemote(entry.parentGroup), Is.False, $"{chapter.Id} 는 받을 것이 없는 챕터인데 원격 그룹에 있다");
                    continue;
                }

                Assert.That(labels, Does.Contain(chapter.DownloadLabel), $"{chapter.DownloadLabel} 라벨이 없다");
                Assert.That(IsRemote(entry.parentGroup), Is.True, $"{chapter.Id} 씬이 원격 그룹에 있지 않다");
                Assert.That(entry.labels, Does.Contain(chapter.DownloadLabel), $"{chapter.Id} 씬 항목에 {chapter.DownloadLabel} 라벨이 없다");
            }

            foreach (var costume in _content.Costumes)
            {
                var entry = FindEntry(costume.PrefabAddress);
                Assert.That(IsRemote(entry.parentGroup) && entry.labels.Contains(ContentAddresses.PatchBootLabel), Is.True,
                    $"{costume.Id} 코스튬은 시작 패치 라벨이 붙은 원격 항목이어야 한다");
            }
        }

        [Test]
        public void LocalGroupsAreStaticAndRemoteGroupsAreNot()
        {
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
        public void CcdLocationIsComplete()
        {
            Assert.That(CcdConfig.ProjectId, Is.Not.Empty);
            foreach (var bucket in new[] { CcdConfig.Windows, CcdConfig.Android, CcdConfig.IOS })
            {
                Assert.That(bucket.Name, Is.Not.Empty);
                Assert.That(bucket.Id, Is.Not.Empty, $"{bucket.Name} 버킷 ID 가 비어 있다");
            }

            var url = CcdConfig.EntryUrl(CcdConfig.Windows, CcdConfig.LatestBadge, "catalog.hash");
            Assert.That(url, Does.Contain(CcdConfig.Windows.Id).And.Contain("/release_by_badge/latest/").And.EndWith("?path=/catalog.hash"));
        }

        private string ReadAddress(string address)
        {
            var path = ResolvePath(address);
            Assert.That(path != null && File.Exists(path), Is.True, $"{address} 파일이 없다");
            return File.ReadAllText(path);
        }

        /// <summary>폴더 항목 주소 아래 상대 경로가 파일 주소다(SPEC 5.2). 가장 긴 폴더 주소부터 맞춘다.</summary>
        private string ResolvePath(string address)
        {
            var folder = FindEntry(address);
            if (folder == null)
                return null;

            return folder.address == address ? folder.AssetPath : $"{folder.AssetPath}/{address.Substring(folder.address.Length + 1)}";
        }

        private AddressableAssetEntry FindEntry(string address)
        {
            AddressableAssetEntry best = null;
            foreach (var group in _settings.groups)
            {
                if (group == null)
                    continue;

                foreach (var entry in group.entries)
                {
                    if (entry.address == address)
                        return entry;

                    // IsFolder 는 저장되지 않고 Groups 창 등이 항목을 훑을 때 채워진다. 새로 연 프로젝트(Jenkins)에서는 비어 있어 에셋 데이터베이스로 본다.
                    if (AssetDatabase.IsValidFolder(entry.AssetPath) && address.StartsWith(entry.address + "/") && (best == null || entry.address.Length > best.address.Length))
                        best = entry;
                }
            }

            return best;
        }

        private bool IsRemote(AddressableAssetGroup group)
        {
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            return bundled != null && bundled.LoadPath.GetName(_settings) == AddressableAssetSettings.kRemoteLoadPath;
        }
    }
}
