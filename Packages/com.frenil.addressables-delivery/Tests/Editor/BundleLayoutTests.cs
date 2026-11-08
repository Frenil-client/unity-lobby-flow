using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace AddressablesDelivery.Tests
{
    /// <summary>
    /// 번들 구성 검사. 같은 에셋이 여러 번들에 복사되지 않는지(Addressables Analyze),
    /// 빌드 씬과 Resources 가 원격 콘텐츠를 직접 참조해 패치를 거치지 않고 플레이어에 들어가지 않는지 본다.
    /// </summary>
    public sealed class BundleLayoutTests
    {
        private const string NoIssues = "No issues found";

        private AddressableAssetSettings _settings;

        [OneTimeSetUp]
        public void LoadSettings()
        {
            _settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(_settings, Is.Not.Null, "Addressables 설정이 없다");
        }

        [Test]
        public void NoAssetIsDuplicatedAcrossBundles()
        {
            // Analyze 의 "Check Duplicate Bundle Dependencies" 규칙. 그룹에 넣지 않은 에셋을 둘 이상의 번들이 쓰면 번들마다 복사된다.
            // 실제 번들 빌드와 같은 의존성 계산을 하므로 수십 초 걸릴 수 있다.
            var rule = new CheckBundleDupeDependencies();
            var results = rule.RefreshAnalysis(_settings);
            // 결과 한 줄은 "그룹:번들:에셋" 이고 하위 에셋마다 반복된다. 에셋별로 묶어 어느 번들들에 들어갔는지만 보인다.
            // 형식이 다른 줄(분석 빌드 실패 등)은 그대로 실패로 남긴다.
            var raw = results.Select(r => r.resultName).Where(name => name != NoIssues).ToList();
            var issues = raw.Select(name => name.Split(':'))
                .Where(parts => parts.Length >= 3)
                .GroupBy(parts => string.Join(":", parts.Skip(2)), parts => parts[1])
                .Select(g => $"{g.Key} → {string.Join(", ", g.Distinct().OrderBy(b => b))}")
                .Concat(raw.Where(name => name.Split(':').Length < 3))
                .OrderBy(line => line)
                .ToList();
            rule.ClearAnalysis();

            Assert.That(issues, Is.Empty, "여러 번들에 복사되는 에셋이 있다(에셋 → 번들)");
        }

        [Test]
        public void BuildScenesAndResourcesDoNotReferenceRemoteContent()
        {
            // 빌드 씬과 Resources 가 직접 참조한 에셋은 플레이어에 들어가 패치로 바꿀 수 없다. 원격 그룹 에셋은 Addressables 로만 쓴다.
            // 로컬 그룹 에셋(폰트 등)은 둘 다 앱에 들어 있어 다운로드와 무관하므로 허용한다.
            var remote = RemoteAssetPaths();
            Assert.That(remote, Is.Not.Empty, "원격 그룹 에셋을 찾지 못했다");

            var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                .Concat(AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.Contains("/Resources/")))
                .ToArray();
            Assert.That(roots, Is.Not.Empty, "빌드 씬이 없다");

            var leaked = AssetDatabase.GetDependencies(roots, true).Where(remote.Contains).OrderBy(p => p).ToList();
            Assert.That(leaked, Is.Empty, "빌드 씬이나 Resources 가 원격 그룹 에셋을 직접 참조한다");
        }

        private HashSet<string> RemoteAssetPaths()
        {
            var paths = new HashSet<string>();
            foreach (var group in _settings.groups)
            {
                var bundled = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
                if (bundled == null || bundled.LoadPath.GetName(_settings) != AddressableAssetSettings.kRemoteLoadPath)
                    continue;

                foreach (var entry in group.entries)
                {
                    if (!AssetDatabase.IsValidFolder(entry.AssetPath))
                    {
                        paths.Add(entry.AssetPath);
                        continue;
                    }

                    foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { entry.AssetPath }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!AssetDatabase.IsValidFolder(path))
                            paths.Add(path);
                    }
                }
            }

            return paths;
        }
    }
}
