using System.Collections;
using System.Text.RegularExpressions;
using LobbyFlow.Loading;
using LobbyFlow.Preview;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LobbyFlow.Tests
{
    /// <summary>
    /// 미리보기 요청 규칙(SPEC 4.2). 마지막 요청만 반영하고, 취소·실패·Dispose 어느 경로에서도 받은 리소스는 정확히 한 번 반환한다.
    /// 로더는 테스트가 완료 순서를 정하는 가짜라 Addressables 와 네트워크를 쓰지 않는다.
    /// </summary>
    public sealed class PreviewPresenterTests
    {
        private GameObject _prefab;
        private Transform _anchor;
        private FakePreviewLoader _loader;
        private CharacterPreviewPresenter _presenter;
        private int _instancesBefore;
        private int _staleBefore;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("PreviewPrefab");
            _anchor = new GameObject("PreviewAnchor").transform;
            _loader = new FakePreviewLoader(_prefab);
            _presenter = new CharacterPreviewPresenter(_loader, _anchor, "Test");
            _instancesBefore = ResourceCounters.LiveInstances;
            _staleBefore = ResourceCounters.StaleReleased;
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
            Object.Destroy(_anchor.gameObject);
            Object.Destroy(_prefab);
        }

        [UnityTest]
        public IEnumerator LatestRequestWinsAndLateResultIsReleased()
        {
            _presenter.Show(new TestSource("a"));
            _presenter.Show(new TestSource("b"));
            Assert.That(_loader.Requests[0].Token.IsCancellationRequested, Is.True, "새 요청이 이전 요청을 취소하지 않았다");

            _loader.Succeed(1);
            yield return Frames();
            Assert.That(_presenter.State, Is.EqualTo(PreviewState.Ready));
            Assert.That(_presenter.Current.PreviewKey, Is.EqualTo("b"));
            Assert.That(_anchor.childCount, Is.EqualTo(1));

            // 취소된 요청의 결과가 늦게 도착해도 표시를 바꾸지 않고 바로 반환한다.
            _loader.Succeed(0);
            yield return Frames();
            Assert.That(_loader.Released, Is.EqualTo(1), "늦게 도착한 결과를 반환하지 않았다");
            Assert.That(_presenter.Current.PreviewKey, Is.EqualTo("b"));
            Assert.That(_anchor.childCount, Is.EqualTo(1));
            Assert.That(ResourceCounters.StaleReleased - _staleBefore, Is.EqualTo(1));

            _presenter.Dispose();
            yield return Frames();
            Assert.That(_loader.Released, Is.EqualTo(_loader.Acquired), "보유 리소스를 모두 반환하지 않았다");
            Assert.That(_anchor.childCount, Is.EqualTo(0));
            Assert.That(ResourceCounters.LiveInstances, Is.EqualTo(_instancesBefore));
        }

        [UnityTest]
        public IEnumerator ResultArrivingAfterDisposeIsReleased()
        {
            _presenter.Show(new TestSource("a"));
            _presenter.Dispose();
            Assert.That(_loader.Requests[0].Token.IsCancellationRequested, Is.True);

            _loader.Succeed(0);
            yield return Frames();
            Assert.That(_loader.Released, Is.EqualTo(1), "화면이 사라진 뒤 도착한 결과를 반환하지 않았다");
            Assert.That(_anchor.childCount, Is.EqualTo(0));
            Assert.That(ResourceCounters.LiveInstances, Is.EqualTo(_instancesBefore));
        }

        [UnityTest]
        public IEnumerator CanceledRequestNeitherFailsNorReplacesNewerOne()
        {
            _presenter.Show(new TestSource("a"));
            _presenter.Show(new TestSource("b"));

            _loader.CompleteCanceled(0);
            yield return Frames();
            Assert.That(_presenter.State, Is.EqualTo(PreviewState.Loading), "취소된 요청이 상태를 바꿨다");
            Assert.That(_presenter.LastError, Is.Null);

            _loader.Succeed(1);
            yield return Frames();
            Assert.That(_presenter.State, Is.EqualTo(PreviewState.Ready));
            Assert.That(_presenter.Current.PreviewKey, Is.EqualTo("b"));
        }

        [UnityTest]
        public IEnumerator FailureCanBeRetried()
        {
            _presenter.Show(new TestSource("a"));
            _loader.Fail(0, new PreviewLoadException("a", "테스트 실패"));
            yield return Frames();
            Assert.That(_presenter.State, Is.EqualTo(PreviewState.Failed));
            Assert.That(_presenter.LastError, Is.TypeOf<PreviewLoadException>());
            Assert.That(_anchor.childCount, Is.EqualTo(0));

            _presenter.Retry();
            Assert.That(_loader.Requests.Count, Is.EqualTo(2), "다시 시도가 새 요청을 보내지 않았다");
            _loader.Succeed(1);
            yield return Frames();
            Assert.That(_presenter.State, Is.EqualTo(PreviewState.Ready));
            Assert.That(_presenter.LastError, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReplacingShownPreviewReleasesPreviousOnce()
        {
            _presenter.Show(new TestSource("a"));
            _loader.Succeed(0);
            yield return Frames();
            Assert.That(_presenter.State, Is.EqualTo(PreviewState.Ready));

            _presenter.Show(new TestSource("b"));
            Assert.That(_loader.Released, Is.EqualTo(1), "교체할 때 이전 리소스를 반환하지 않았다");
            yield return Frames();
            Assert.That(_anchor.childCount, Is.EqualTo(0), "교체할 때 이전 인스턴스를 파괴하지 않았다");

            _loader.Succeed(1);
            yield return Frames();
            Assert.That(_anchor.childCount, Is.EqualTo(1));
            Assert.That(_loader.Released, Is.EqualTo(1));
            Assert.That(ResourceCounters.LiveInstances - _instancesBefore, Is.EqualTo(1));
        }

        /// <summary>완료 뒤 이어지는 실행과 Destroy 가 반영될 때까지 두 프레임 기다린다.</summary>
        private static IEnumerator Frames()
        {
            yield return null;
            yield return null;
        }

        [Test]
        public void PreviewAssetIgnoresSecondRelease()
        {
            var released = 0;
            var asset = new PreviewAsset("a", _prefab, () => released++);

            asset.Release();
            LogAssert.Expect(LogType.Warning, new Regex("이중 해제"));
            asset.Release();

            Assert.That(released, Is.EqualTo(1));
            Assert.That(asset.IsReleased, Is.True);
        }
    }
}
