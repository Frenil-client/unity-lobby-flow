using System;
using System.Collections;
using System.Threading;
using LobbyFlow.Content;
using LobbyFlow.Patching;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LobbyFlow.Tests
{
    /// <summary>
    /// 모의 패치 서비스(SPEC 6.4). 끊긴 다운로드를 다시 시도하면 받은 곳부터 이어 받고, 오프라인 두 경로가 갈리는지 본다.
    /// 화면 흐름 검증용 모의 조건이며 실제 네트워크 장애 검증이 아니다.
    /// </summary>
    public sealed class SimulatedPatchServiceTests
    {
        private const string Label = "chapter02";

        [SetUp]
        public void SetUp() => ResetSettings();

        [TearDown]
        public void TearDown() => ResetSettings();

        [UnityTest]
        public IEnumerator InterruptedDownloadResumesOnRetry() => AsyncTest.Run(async () =>
        {
            PatchSimulationSettings.Mode = PatchSimulationMode.UpdateAvailable;
            PatchSimulationSettings.FailDownloadOnce = true;
            var service = new SimulatedPatchService();
            var total = await service.GetDownloadSizeAsync(Label, CancellationToken.None);
            Assert.That(total, Is.GreaterThan(0));

            var lastRatio = 0f;
            void Track(DownloadProgress progress)
            {
                Assert.That(progress.Ratio, Is.GreaterThanOrEqualTo(lastRatio), "진행률이 뒤로 갔다");
                lastRatio = progress.Ratio;
            }

            Exception error = null;
            try
            {
                await service.DownloadAsync(Label, Track, CancellationToken.None);
            }
            catch (PatchNetworkException e)
            {
                error = e;
            }

            Assert.That(error, Is.Not.Null, "끊김을 주입했는데 다운로드가 끝났다");
            Assert.That(((PatchNetworkException)error).IsInjected, Is.True);

            var remaining = await service.GetDownloadSizeAsync(Label, CancellationToken.None);
            Assert.That(remaining, Is.GreaterThan(0).And.LessThan(total), "끊기기 전까지 받은 양이 남지 않았다");

            await service.DownloadAsync(Label, Track, CancellationToken.None);
            Assert.That(await service.GetDownloadSizeAsync(Label, CancellationToken.None), Is.EqualTo(0));
            Assert.That(lastRatio, Is.EqualTo(1f));
        });

        [UnityTest]
        public IEnumerator OfflineWithCacheStartsWithoutPatch() => AsyncTest.Run(async () =>
        {
            PatchSimulationSettings.Mode = PatchSimulationMode.OfflineWithCache;
            var service = new SimulatedPatchService();

            Assert.That(await service.HasCachedContentAsync(CancellationToken.None), Is.True);
            Assert.That(await CheckFailsAsync(service), Is.True, "오프라인인데 카탈로그 확인이 성공했다");
        });

        [UnityTest]
        public IEnumerator OfflineFirstRunHasNoCachedContent() => AsyncTest.Run(async () =>
        {
            PatchSimulationSettings.Mode = PatchSimulationMode.OfflineFirstRun;
            var service = new SimulatedPatchService();

            Assert.That(await service.HasCachedContentAsync(CancellationToken.None), Is.False);
            Assert.That(await CheckFailsAsync(service), Is.True);
        });

        [UnityTest]
        public IEnumerator UpToDateHasNothingToDownload() => AsyncTest.Run(async () =>
        {
            PatchSimulationSettings.Mode = PatchSimulationMode.UpToDate;
            var service = new SimulatedPatchService();

            var result = await service.CheckAsync(CancellationToken.None);
            Assert.That(result.HasUpdate, Is.False);
            Assert.That(await service.GetDownloadSizeAsync(ContentAddresses.PatchBootLabel, CancellationToken.None), Is.EqualTo(0));
        });

        private static async Awaitable<bool> CheckFailsAsync(SimulatedPatchService service)
        {
            try
            {
                await service.CheckAsync(CancellationToken.None);
                return false;
            }
            catch (PatchNetworkException)
            {
                return true;
            }
        }

        private static void ResetSettings()
        {
            PatchSimulationSettings.Mode = PatchSimulationMode.Addressables;
            PatchSimulationSettings.FailDownloadOnce = false;
        }
    }
}
