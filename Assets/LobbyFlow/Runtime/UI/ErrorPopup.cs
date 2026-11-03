using System.Threading;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>재시도 또는 종료를 고르게 하는 오류 팝업. 뒤로가기와 Dim 클릭으로 닫히지 않는다.</summary>
    public sealed class ErrorPopup : UIPopup
    {
        [SerializeField] private Text _message;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _quitButton;

        protected override UIViewOptions DefaultOptions => new() { BlockClose = true };

        private void Awake()
        {
            _retryButton.onClick.AddListener(CloseConfirmed);
            _quitButton.onClick.AddListener(CloseCancelled);
        }

        /// <summary>오류를 보여 주고 사용자의 선택을 기다린다. 재시도를 고르면 true. 팝업을 열지 못하면 false.</summary>
        /// <param name="declineLabel">거절 버튼 문구. Boot 는 종료, 챕터 진입은 취소다.</param>
        public static async Awaitable<bool> AskRetryAsync(string message, CancellationToken cancellationToken, string declineLabel = "종료")
        {
            var manager = UIManager.Instance;
            if (manager == null)
                return false;

            var popup = await manager.OpenAsync<ErrorPopup>(cancellationToken);
            if (popup == null)
                return false;

            popup._message.text = message;
            popup._quitButton.GetComponentInChildren<Text>(true).text = declineLabel;
            var result = await popup.WaitForCloseAsync();
            return result.IsConfirmed;
        }
    }
}
