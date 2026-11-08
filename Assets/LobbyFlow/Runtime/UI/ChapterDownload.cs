using System;
using System.Threading;
using AddressablesDelivery.Patching;
using LobbyFlow.Content;
using UISystem;
using UnityEngine;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 원격 챕터에 들어가기 전에 받을 것이 있으면 다운로드 팝업으로 받는다(SPEC 6.1, 6.4).
    /// 실패하거나 취소하면 챕터 진입만 취소하고 로비에 남는다.
    /// </summary>
    public static class ChapterDownload
    {
        private const float CompletedHoldSeconds = 0.3f;

        /// <summary>챕터 콘텐츠를 다 받았다. 로비의 추가 다운로드 표시를 지우는 데 쓴다.</summary>
        public static event Action<ChapterRecord> Downloaded;

        /// <summary>바로 들어가도 되면 true.</summary>
        public static async Awaitable<bool> EnsureAsync(ChapterRecord chapter)
        {
            var cancellationToken = CancellationToken.None;
            var patch = LobbyServices.PatchService;

            long bytes;
            try
            {
                bytes = await patch.GetDownloadSizeAsync(chapter.DownloadLabel, cancellationToken);
            }
            catch (PatchNetworkException e)
            {
                LobbyLog.Warning($"챕터 다운로드 크기 확인 실패 {chapter.Id}: {e.Message}");
                MessageToast.Show("챕터 콘텐츠를 확인하지 못했습니다.");
                return false;
            }

            if (bytes == 0)
                return true;

            var manager = UIManager.Instance;
            var popup = manager != null ? await manager.OpenAsync<PatchPopup>(cancellationToken) : null;
            if (popup == null)
                return false;

            var onCellular = Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork;
            if (!await popup.AskDownloadAsync(chapter.Name, $"챕터 콘텐츠 {ByteFormat.ToMegabytes(bytes)}를 받아야 합니다.", "취소", onCellular))
            {
                await manager.CloseAsync(popup);
                return false;
            }

            while (true)
            {
                try
                {
                    LobbyLog.Info($"챕터 다운로드 시작 {chapter.Id}");
                    await patch.DownloadAsync(chapter.DownloadLabel, popup.ShowProgress, cancellationToken);
                    break;
                }
                catch (PatchNetworkException e)
                {
                    LobbyLog.Warning($"챕터 다운로드 실패 {chapter.Id}: {e.Message}");
                    if (!await ErrorPopup.AskRetryAsync("다운로드가 중단됐습니다.\n다시 시도하면 받은 부분부터 이어서 받습니다.", cancellationToken, "취소"))
                    {
                        await manager.CloseAsync(popup);
                        return false;
                    }
                }
            }

            LobbyLog.Info($"챕터 다운로드 완료 {chapter.Id}");
            Downloaded?.Invoke(chapter);
            popup.ShowCompleted();
            await Awaitable.WaitForSecondsAsync(CompletedHoldSeconds);
            await manager.CloseAsync(popup);
            return true;
        }
    }
}
