using System.Collections.Generic;
using LobbyFlow.Content;
using LobbyFlow.Loading;
using LobbyFlow.Patching;
using LobbyFlow.Scenes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 지연·실패 주입과 모의 패치 조건을 바꾸는 테스트 메뉴, 리소스 집계 표시. 개발 빌드에서만 남는다.
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
        [SerializeField] private Button _patchModeButton;
        [SerializeField] private Button _patchFailButton;
        [SerializeField] private Button _clearCacheButton;
        [SerializeField] private Button _rebootButton;
        [SerializeField] private TMP_Text _counters;

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
            _patchModeButton.onClick.AddListener(PatchSimulationSettings.CycleMode);
            _patchFailButton.onClick.AddListener(() => PatchSimulationSettings.FailDownloadOnce = !PatchSimulationSettings.FailDownloadOnce);
            _clearCacheButton.onClick.AddListener(ClearCache);
            _rebootButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Boot));
        }

        /// <summary>시작 패치와 원격 챕터 라벨의 캐시를 지운다. 데이터를 아직 읽지 않았으면 시작 패치만 지운다.</summary>
        private static void ClearCache()
        {
            var labels = new List<string> { ContentAddresses.PatchBootLabel };
            if (LobbyServices.Content != null)
            {
                foreach (var chapter in LobbyServices.Content.Chapters)
                {
                    if (chapter.RequiresDownload)
                        labels.Add(chapter.DownloadLabel);
                }
            }

            LobbyServices.PatchService.ClearCache(labels);
            LobbyLog.Info($"다운로드 캐시 삭제: {string.Join(", ", labels)}");
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
            SetLabel(_patchModeButton, PatchSimulationSettings.Mode switch
            {
                PatchSimulationMode.UpToDate => "모의 패치: 최신",
                PatchSimulationMode.OfflineWithCache => "모의 패치: 오프라인(캐시 있음)",
                PatchSimulationMode.OfflineFirstRun => "모의 패치: 오프라인(첫 실행)",
                PatchSimulationMode.UpdateAvailable => "모의 패치: 업데이트 있음",
                _ => "패치: 실제 Addressables",
            });
            SetLabel(_patchFailButton, PatchSimulationSettings.FailDownloadOnce ? "다운로드 끊김: 다음 1회" : "다운로드 끊김: 없음");
            SetLabel(_clearCacheButton, "다운로드 캐시 삭제");
            SetLabel(_rebootButton, "Boot 다시 실행");

            // 같은 값이면 TMP_Text 에 다시 쓰지 않아 매 프레임 메시 재생성을 막는다.
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
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text != text)
                label.text = text;
        }
    }
}
