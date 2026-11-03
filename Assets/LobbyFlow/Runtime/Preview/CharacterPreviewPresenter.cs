using System;
using System.Threading;
using LobbyFlow.Characters;
using LobbyFlow.Loading;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LobbyFlow.Preview
{
    public enum PreviewState
    {
        Idle,
        Loading,
        Ready,
        Failed,
    }

    /// <summary>
    /// 한 자리(anchor)에 캐릭터 미리보기 하나를 띄운다. 화면이 소유하고, 화면이 닫히거나 파괴될 때 Dispose 한다.
    /// <list type="bullet">
    /// <item>요청마다 번호를 매기고, 결과가 도착했을 때 마지막 요청이 아니면 반영하지 않고 반환한다.</item>
    /// <item>새 요청이나 Dispose 는 이전 요청을 취소한다. 취소는 요청일 뿐이라 결과가 도착할 수 있으므로 위 확인을 항상 거친다.</item>
    /// <item>표시 중인 리소스와 인스턴스는 이 객체가 소유한다. 교체·Dispose 때 인스턴스를 파괴하고 리소스를 반환한다.</item>
    /// </list>
    /// </summary>
    public sealed class CharacterPreviewPresenter : IDisposable
    {
        private readonly IPreviewAssetLoader _loader;
        private readonly Transform _anchor;
        private readonly string _owner;

        private CancellationTokenSource _requestCts;
        private int _requestId;
        private PreviewAsset _asset;
        private GameObject _instance;
        private bool _disposed;

        public CharacterPreviewPresenter(IPreviewAssetLoader loader, Transform anchor, string owner)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _anchor = anchor;
            _owner = owner;
        }

        public event Action StateChanged;

        public PreviewState State { get; private set; }
        public CharacterDefinition Current { get; private set; }
        public Exception LastError { get; private set; }

        public void Show(CharacterDefinition character)
        {
            if (_disposed || character == null)
                return;

            Run(character);
        }

        public void Retry()
        {
            if (Current != null && State == PreviewState.Failed)
                Show(Current);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CancelRequest();
            ReleaseCurrent();
            StateChanged = null;
            LobbyLog.Info($"{_owner} 미리보기 해제");
        }

        private async void Run(CharacterDefinition character)
        {
            CancelRequest();
            ReleaseCurrent();

            var requestId = ++_requestId;
            var cts = new CancellationTokenSource();
            _requestCts = cts;
            Current = character;
            LastError = null;
            SetState(PreviewState.Loading);
            LobbyLog.Info($"{_owner} #{requestId} 요청 {character.Id}");

            PreviewAsset asset;
            try
            {
                asset = await _loader.LoadAsync(character, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 로더가 받은 리소스를 이미 반환했다. 이 요청을 끊은 쪽이 상태를 정한다.
                LobbyLog.Info($"{_owner} #{requestId} 취소 완료 {character.Id}");
                return;
            }
            catch (Exception e)
            {
                if (!IsCurrent(requestId))
                {
                    LobbyLog.Info($"{_owner} #{requestId} 무효 요청의 실패 무시 {character.Id}");
                    return;
                }

                LastError = e;
                LobbyLog.Warning($"{_owner} #{requestId} 실패 {e.Message}");
                SetState(PreviewState.Failed);
                return;
            }

            // 취소 요청 뒤에도 결과가 올 수 있다. 소유권은 받은 순간 여기로 넘어왔으므로 반영하지 않을 때는 직접 반환한다.
            if (!IsCurrent(requestId))
            {
                asset.Release();
                ResourceCounters.StaleResultReleased();
                LobbyLog.Info($"{_owner} #{requestId} 늦게 도착한 결과 반환 {character.Id}");
                return;
            }

            if (_anchor == null)
            {
                asset.Release();
                LastError = new PreviewLoadException(character.Id, "미리보기 위치가 사라졌다");
                SetState(PreviewState.Failed);
                return;
            }

            _asset = asset;
            _instance = Object.Instantiate(asset.Prefab, _anchor, false);
            ResourceCounters.InstanceCreated();
            LobbyLog.Info($"{_owner} #{requestId} 표시 {character.Id}");
            SetState(PreviewState.Ready);
        }

        private bool IsCurrent(int requestId) => !_disposed && requestId == _requestId;

        private void CancelRequest()
        {
            if (_requestCts == null)
                return;

            _requestCts.Cancel();
            _requestCts.Dispose();
            _requestCts = null;
        }

        private void ReleaseCurrent()
        {
            // 씬 언로드로 Unity 가 먼저 파괴했어도 이 객체의 소유 기록은 남아 있다. 참조 자체로 판단해 집계를 맞춘다.
            if (!ReferenceEquals(_instance, null))
            {
                if (_instance != null)
                    Object.Destroy(_instance);

                _instance = null;
                ResourceCounters.InstanceDestroyed();
            }

            if (_asset != null)
            {
                _asset.Release();
                _asset = null;
            }
        }

        private void SetState(PreviewState state)
        {
            State = state;
            StateChanged?.Invoke();
        }
    }
}
