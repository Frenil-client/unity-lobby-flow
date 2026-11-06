using System;
using System.Collections;
using System.Threading;
using LobbyFlow.Loading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace LobbyFlow.Tests
{
    /// <summary>
    /// 지연·실패 주입 로더(테스트 메뉴가 끼우는 것). 주입한 실패나 지연 중 취소에서도 이미 받은 리소스를 반환해야 한다.
    /// 모의 조건이므로 실제 네트워크 장애 검증과 구분한다.
    /// </summary>
    public sealed class FaultInjectionTests
    {
        private GameObject _prefab;
        private FakePreviewLoader _inner;
        private FaultInjectingPreviewLoader _loader;

        [SetUp]
        public void SetUp()
        {
            ResetSettings();
            _prefab = new GameObject("PreviewPrefab");
            _inner = new FakePreviewLoader(_prefab) { AutoComplete = true };
            _loader = new FaultInjectingPreviewLoader(_inner);
        }

        [TearDown]
        public void TearDown()
        {
            ResetSettings();
            Object.Destroy(_prefab);
        }

        [UnityTest]
        public IEnumerator InjectedFailureReleasesLoadedAsset() => AsyncTest.Run(async () =>
        {
            LoadFaultSettings.FailMode = LoadFailMode.Once;

            var error = await CatchAsync(_loader.LoadAsync(new TestSource("a"), CancellationToken.None));

            Assert.That(error, Is.TypeOf<PreviewLoadException>());
            Assert.That(((PreviewLoadException)error).IsInjected, Is.True, "주입한 실패가 실제 실패와 구분되지 않는다");
            Assert.That(_inner.Released, Is.EqualTo(_inner.Acquired), "주입한 실패에서 받은 리소스를 반환하지 않았다");
            Assert.That(LoadFaultSettings.FailMode, Is.EqualTo(LoadFailMode.None), "1회 실패가 다음 요청에도 남았다");
        });

        [UnityTest]
        public IEnumerator CancelDuringInjectedDelayReleasesLoadedAsset() => AsyncTest.Run(async () =>
        {
            SetDelay(0.5f);
            using var cts = new CancellationTokenSource();

            var load = _loader.LoadAsync(new TestSource("a"), cts.Token);
            await Awaitable.NextFrameAsync();
            cts.Cancel();
            var error = await CatchAsync(load);

            Assert.That(error, Is.InstanceOf<OperationCanceledException>());
            Assert.That(_inner.Acquired, Is.EqualTo(1));
            Assert.That(_inner.Released, Is.EqualTo(1), "지연 중 취소에서 받은 리소스를 반환하지 않았다");
        });

        [UnityTest]
        public IEnumerator IgnoredCancellationHandsResultToCaller() => AsyncTest.Run(async () =>
        {
            // 취소를 무시하면 결과가 그대로 돌아온다. 소유권은 호출한 쪽에 있고, 무효 요청이면 호출한 쪽이 반환한다.
            SetDelay(0.5f);
            LoadFaultSettings.IgnoreCancellation = true;
            using var cts = new CancellationTokenSource();

            var load = _loader.LoadAsync(new TestSource("a"), cts.Token);
            await Awaitable.NextFrameAsync();
            cts.Cancel();
            var asset = await load;

            Assert.That(asset, Is.Not.Null);
            Assert.That(_inner.Released, Is.EqualTo(0), "로더가 넘긴 결과를 스스로 반환했다");
            asset.Release();
            Assert.That(_inner.Released, Is.EqualTo(1));
        });

        private static async Awaitable<Exception> CatchAsync(Awaitable<PreviewAsset> load)
        {
            try
            {
                var asset = await load;
                asset.Release();
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        private static void SetDelay(float seconds)
        {
            for (var i = 0; i < LoadFaultSettings.DelayOptions.Length && !Mathf.Approximately(LoadFaultSettings.DelaySeconds, seconds); i++)
                LoadFaultSettings.CycleDelay();

            Assert.That(LoadFaultSettings.DelaySeconds, Is.EqualTo(seconds), $"지연 선택지에 {seconds}초가 없다");
        }

        private static void ResetSettings()
        {
            SetDelay(0f);
            LoadFaultSettings.DelayOnce = false;
            LoadFaultSettings.FailMode = LoadFailMode.None;
            LoadFaultSettings.IgnoreCancellation = false;
        }
    }
}
