using System;
using System.Threading;
using AddressablesDelivery;
using AddressablesDelivery.Patching;
using LobbyFlow.Content;
using LobbyFlow.Scenes;
using TMPro;
using UISystem;
using UnityEngine;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 첫 씬. 카탈로그 변경을 확인하고, 받을 것이 있으면 패치 팝업으로 받은 뒤 데이터를 읽고 로비로 간다(SPEC 6.2).
    /// 이 화면과 팝업은 앱에 내장된 리소스만 쓴다.
    /// </summary>
    public sealed class BootScreen : UIScreen
    {
        private const float CompletedHoldSeconds = 0.4f;

        [SerializeField] private TMP_Text _status;

        private long _downloadBytes;

        private enum BootStep
        {
            Download,
            Skip,
            Quit,
        }

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
            LobbyLog.Info($"Boot 시작, 패치 서비스 {patch.GetType().Name}, CCD 배지 {DescribeBadge()}");

            var check = await CheckWithRetryAsync(patch, cancellationToken);
            if (check == BootStep.Quit)
                return;

            if (check == BootStep.Download && !await DownloadAsync(patch, cancellationToken))
                return;

            if (!await LoadContentWithRetryAsync(cancellationToken))
                return;

            _status.text = "로비로 이동합니다.";
            SceneFlow.Load(SceneFlow.Lobby);
        }

        private static string DescribeBadge() =>
            CcdLocation.PendingBadge == CcdLocation.Badge ? CcdLocation.Badge : $"{CcdLocation.Badge} → {CcdLocation.PendingBadge}";

        private async Awaitable<BootStep> CheckWithRetryAsync(IPatchService patch, CancellationToken cancellationToken)
        {
            while (true)
            {
                _status.text = "업데이트 확인 중…";
                try
                {
                    var result = await patch.CheckAsync(cancellationToken);
                    LobbyLog.Info($"Boot 카탈로그 확인 완료, 갱신 {result.CatalogUpdated}, 받을 크기 {ByteFormat.Format(result.DownloadBytes)}({result.DownloadBytes}바이트)");
                    _downloadBytes = result.DownloadBytes;
                    return result.HasUpdate ? BootStep.Download : BootStep.Skip;
                }
                catch (PatchNetworkException e)
                {
                    LobbyLog.Warning($"Boot 카탈로그 확인 실패: {e.Message}");

                    // 시작 패치를 이미 다 받아 두었으면 저장된 콘텐츠로 들어간다(SPEC 6.4).
                    if (await patch.HasCachedContentAsync(cancellationToken))
                    {
                        LobbyServices.PendingNotice = "서버에 연결할 수 없어 저장된 콘텐츠로 시작합니다.";
                        return BootStep.Skip;
                    }

                    _status.text = "서버에 연결할 수 없습니다.";
                    if (!await ErrorPopup.AskRetryAsync("서버에 연결할 수 없습니다.\n네트워크 연결을 확인해 주세요.", cancellationToken))
                    {
                        AppQuit.Quit();
                        return BootStep.Quit;
                    }
                }
            }
        }

        /// <summary>받기를 마쳤으면 true, 종료를 골랐으면 false.</summary>
        private async Awaitable<bool> DownloadAsync(IPatchService patch, CancellationToken cancellationToken)
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
            var message = $"추가 콘텐츠 {ByteFormat.Format(_downloadBytes)}를 받아야 합니다.";
            if (!await popup.AskDownloadAsync("업데이트", message, "종료", onCellular))
            {
                AppQuit.Quit();
                return false;
            }

            while (true)
            {
                try
                {
                    LobbyLog.Info("Boot 패치 다운로드 시작");
                    await patch.DownloadAsync(ContentAddresses.PatchBootLabel, popup.ShowProgress, cancellationToken);
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

        /// <summary>데이터를 읽었으면 true, 종료를 골랐으면 false.</summary>
        private async Awaitable<bool> LoadContentWithRetryAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                _status.text = "데이터 읽는 중…";
                try
                {
                    await LobbyServices.ReloadContentAsync();
                    return true;
                }
                catch (ContentDataException e)
                {
                    LobbyLog.Error($"데이터 오류: {e.Message}");
                    if (!await ErrorPopup.AskRetryAsync($"게임 데이터를 읽지 못했습니다.\n{e.Message}", cancellationToken))
                    {
                        AppQuit.Quit();
                        return false;
                    }
                }
            }
        }
    }
}
