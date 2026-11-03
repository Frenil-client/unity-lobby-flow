using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>짧은 안내 문구. 입력을 막지 않고 정해진 시간이 지나면 스스로 닫힌다.</summary>
    public sealed class MessageToast : UIToast
    {
        private const float DefaultSeconds = 2.5f;

        [SerializeField] private Text _message;

        private float _closeAt;

        public static async void Show(string message, float seconds = DefaultSeconds)
        {
            var manager = UIManager.Instance;
            if (manager == null || string.IsNullOrEmpty(message))
                return;

            // 이미 떠 있으면 문구와 시간만 바꾼다. 풀링되므로 같은 인스턴스를 다시 쓴다.
            var toast = manager.Find<MessageToast>() ?? await manager.OpenAsync<MessageToast>();
            if (toast == null)
                return;

            toast._message.text = message;
            toast._closeAt = Time.unscaledTime + seconds;
        }

        // 문구를 넣기 전에 Update 가 먼저 돌아 바로 닫히지 않게 한다. 실제 시간은 Show 가 정한다.
        protected override void OnOpened() => _closeAt = float.PositiveInfinity;

        private void Update()
        {
            if (IsOpen && Time.unscaledTime >= _closeAt)
                Close(UICloseReason.ClosedByService);
        }
    }
}
