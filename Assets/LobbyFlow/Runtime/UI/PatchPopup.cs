using LobbyFlow.Patching;
using TMPro;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 패치 확인과 다운로드 진행 표시. 확인 단계에서 받을 크기를 보여 주고, 다운로드를 고르면 같은 팝업에서 진행률을 갱신한다.
    /// 앱에 내장된 UI 다(SPEC 6.3). 뒤로가기와 Dim 클릭으로 닫히지 않는다.
    /// </summary>
    public sealed class PatchPopup : UIPopup
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private GameObject _confirmGroup;
        [SerializeField] private Button _downloadButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private GameObject _progressGroup;
        [SerializeField] private RectTransform _progressFill;
        [SerializeField] private TMP_Text _percent;
        [SerializeField] private TMP_Text _bytes;

        private AwaitableCompletionSource<bool> _choice;

        protected override UIViewOptions DefaultOptions => new() { BlockClose = true };

        private void Awake()
        {
            _downloadButton.onClick.AddListener(() => Choose(true));
            _quitButton.onClick.AddListener(() => Choose(false));
        }

        /// <summary>받을 크기 안내를 보여 주고 다운로드·거절 선택을 기다린다. 다운로드를 고르면 true.</summary>
        /// <param name="declineLabel">거절 버튼 문구. Boot 는 종료, 챕터 진입은 취소다.</param>
        public Awaitable<bool> AskDownloadAsync(string title, string message, string declineLabel, bool onCellular)
        {
            _title.text = title;
            _message.text = message + (onCellular ? "\n모바일 데이터로 연결돼 있습니다." : string.Empty);
            _quitButton.GetComponentInChildren<TMP_Text>(true).text = declineLabel;
            _confirmGroup.SetActive(true);
            _progressGroup.SetActive(false);

            _choice = new AwaitableCompletionSource<bool>();
            return _choice.Awaitable;
        }

        public void ShowProgress(DownloadProgress progress)
        {
            if (_confirmGroup.activeSelf)
            {
                _title.text = "받는 중";
                _message.text = "잠시만 기다려 주세요.";
                _confirmGroup.SetActive(false);
                _progressGroup.SetActive(true);
            }

            var anchorMax = _progressFill.anchorMax;
            anchorMax.x = progress.Ratio;
            _progressFill.anchorMax = anchorMax;
            _percent.text = $"{progress.Ratio * 100f:0}%";
            _bytes.text = $"{ByteFormat.ToMegabytes(progress.DownloadedBytes)} / {ByteFormat.ToMegabytes(progress.TotalBytes)}";
        }

        public void ShowCompleted()
        {
            _title.text = "완료";
            _message.text = "업데이트를 마쳤습니다.";
        }

        // 선택을 기다리는 중에 씬 언로드 등으로 닫히면 종료를 고른 것으로 끝내 기다리는 쪽이 멈추지 않게 한다.
        protected override void OnClosing(UIResult result) => Choose(false);

        private void Choose(bool download)
        {
            var choice = _choice;
            _choice = null;
            choice?.SetResult(download);
        }
    }
}
