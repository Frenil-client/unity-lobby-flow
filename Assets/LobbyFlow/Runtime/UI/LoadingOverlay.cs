using TMPro;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>씬 전환 중 입력을 막고 진행률을 보여 준다. 뒤로가기로 닫히지 않는다.</summary>
    public sealed class LoadingOverlay : UIOverlay
    {
        [SerializeField] private TMP_Text _message;

        // 스프라이트 없이 채우기 위해 Image.fillAmount 대신 오른쪽 앵커를 움직인다.
        [SerializeField] private RectTransform _progressFill;

        protected override void OnOpened() => SetProgress(0f);

        public void SetMessage(string message) => _message.text = message;

        /// <summary>0~1 비율.</summary>
        public void SetProgress(float ratio)
        {
            var anchorMax = _progressFill.anchorMax;
            anchorMax.x = Mathf.Clamp01(ratio);
            _progressFill.anchorMax = anchorMax;
        }
    }
}
