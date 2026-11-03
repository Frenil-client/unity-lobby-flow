using System;
using System.Threading;
using LobbyFlow.Patching;
using LobbyFlow.Scenes;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 첫 씬. 카탈로그 변경을 확인하고, 받을 것이 있으면 패치 팝업으로 받은 뒤 로비로 간다(SPEC 6.2).
    /// 이 화면과 팝업은 앱에 내장된 리소스만 쓴다.
    /// </summary>
    public sealed class BootScreen : UIScreen
    {
        private const float CompletedHoldSeconds = 0.4f;

        [SerializeField] private Text _status;

        private async void Start()
        {
            try
            {
                await RunAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Boot 씬을 떠나면서 끊겼다. 다음 씬이 이어받는다.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _status.text = "시작하지 못했습니다.";
            }
        }

        private async Awaitable RunAsync(CancellationToken cancellationToken)
        {
            var patch = LobbyServices.PatchService;

            var check = await CheckWithRetryAsync(patch, cancellationToken);
            if (check == null)
                return;

            if (check.Value.HasUpdate && !await DownloadAsync(patch, check.Value.DownloadBytes, cancellationToken))
                return;

            EnterLobby();
        }

        /// <summary>확인에 성공하면 결과, 오프라인 진입이나 종료로 흐름이 끝났으면 null.</summary>
        private async Awaitable<PatchCheckResult?> CheckWithRetryAsync(IPatchService patch, CancellationToken cancellationToken)
        {
            while (true)
            {
                _status.text = "업데이트 확인 중…";
                try
                {
                    var result = await patch.CheckAsync(cancellationToken);
                    LobbyLog.Info($"Boot 카탈로그 확인 완료, 받을 크기 {ByteFormat.ToMegabytes(result.DownloadBytes)}");
                    return result;
                }
                catch (PatchNetworkException e)
                {
                    LobbyLog.Warning($"Boot 카탈로그 확인 실패: {e.Message}");

                    // 첫 실행이 아니면 저장된 콘텐츠로 들어간다(SPEC 6.4).
                    if (patch.HasCachedContent)
                    {
                        LobbyServices.PendingNotice = "서버에 연결할 수 없어 저장된 콘텐츠로 시작합니다.";
                        EnterLobby();
                        return null;
                    }

                    _status.text = "서버에 연결할 수 없습니다.";
                    if (!await ErrorPopup.AskRetryAsync("서버에 연결할 수 없습니다.\n네트워크 연결을 확인해 주세요.", cancellationToken))
                    {
                        AppQuit.Quit();
                        return null;
                    }
                }
            }
        }

        /// <summary>받기를 마쳤으면 true, 종료를 골랐으면 false.</summary>
        private async Awaitable<bool> DownloadAsync(IPatchService patch, long bytes, CancellationToken cancellationToken)
        {
            var manager = UIManager.Instance;
            var popup = manager != null ? await manager.OpenAsync<PatchPopup>(cancellationToken) : null;
            if (popup == null)
            {
                LobbyLog.Error("패치 팝업을 열지 못했다");
                return false;
            }

            _status.text = "업데이트가 있습니다.";
            var onCellular = Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork;
            if (!await popup.AskDownloadAsync(bytes, onCellular))
            {
                AppQuit.Quit();
                return false;
            }

            while (true)
            {
                try
                {
                    LobbyLog.Info("Boot 패치 다운로드 시작");
                    await patch.DownloadAsync(popup.ShowProgress, cancellationToken);
                    break;
                }
                catch (PatchNetworkException e)
                {
                    LobbyLog.Warning($"Boot 패치 다운로드 실패: {e.Message}");
                    if (!await ErrorPopup.AskRetryAsync("다운로드가 중단됐습니다.\n다시 시도하면 받은 부분부터 이어서 받습니다.", cancellationToken))
                    {
                        AppQuit.Quit();
                        return false;
                    }
                }
            }

            LobbyLog.Info("Boot 패치 다운로드 완료");
            popup.ShowCompleted();
            await Awaitable.WaitForSecondsAsync(CompletedHoldSeconds, cancellationToken);
            return true;
        }

        private void EnterLobby()
        {
            _status.text = "로비로 이동합니다.";
            SceneFlow.Load(SceneFlow.Lobby);
        }
    }
}
