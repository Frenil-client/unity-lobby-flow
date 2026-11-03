using LobbyFlow.Loading;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 지연·실패 주입 테스트 메뉴와 리소스 집계 표시. 개발 빌드에서만 남는다.
    /// UI 스택 밖의 독립 Canvas 로 두어 팝업 Dim 아래에서도 조작할 수 있게 하고, 씬을 넘어 하나만 유지한다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class DebugLoadPanel : MonoBehaviour
    {
        private static DebugLoadPanel _instance;

        [SerializeField] private GameObject _body;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private Button _delayButton;
        [SerializeField] private Button _delayOnceButton;
        [SerializeField] private Button _failButton;
        [SerializeField] private Button _ignoreCancelButton;
        [SerializeField] private Text _counters;

        private string _shown;

        private void Awake()
        {
            if (!Debug.isDebugBuild || (_instance != null && _instance != this))
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _toggleButton.onClick.AddListener(() => _body.SetActive(!_body.activeSelf));
            _delayButton.onClick.AddListener(LoadFaultSettings.CycleDelay);
            _delayOnceButton.onClick.AddListener(() => LoadFaultSettings.DelayOnce = !LoadFaultSettings.DelayOnce);
            _failButton.onClick.AddListener(LoadFaultSettings.CycleFailMode);
            _ignoreCancelButton.onClick.AddListener(() => LoadFaultSettings.IgnoreCancellation = !LoadFaultSettings.IgnoreCancellation);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!_body.activeSelf)
                return;

            SetLabel(_delayButton, $"지연 {LoadFaultSettings.DelaySeconds:0.#}초");
            SetLabel(_delayOnceButton, LoadFaultSettings.DelayOnce ? "지연 적용: 다음 1회" : "지연 적용: 매번");
            SetLabel(_failButton, LoadFaultSettings.FailMode switch
            {
                LoadFailMode.Once => "실패: 다음 1회",
                LoadFailMode.Always => "실패: 항상",
                _ => "실패: 없음",
            });
            SetLabel(_ignoreCancelButton, LoadFaultSettings.IgnoreCancellation ? "취소 무시: 켬" : "취소 무시: 끔");

            // 같은 값이면 Text 에 다시 쓰지 않아 매 프레임 메시 재생성을 막는다.
            var text = $"보유 리소스 {ResourceCounters.LiveAssets} · 인스턴스 {ResourceCounters.LiveInstances}\n" +
                       $"누적 로드 {ResourceCounters.TotalAcquired} · 늦은 결과 반환 {ResourceCounters.StaleReleased}";
            if (text != _shown)
            {
                _shown = text;
                _counters.text = text;
            }
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null && label.text != text)
                label.text = text;
        }
    }
}
