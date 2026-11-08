using System.Collections.Generic;
using AddressablesDelivery;
using AddressablesDelivery.Loading;
using AddressablesDelivery.Patching;
using LobbyFlow.Content;
using LobbyFlow.Diagnostics;
using LobbyFlow.Loading;
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
        [SerializeField] private Button _contentBadgeButton;
        [SerializeField] private Button _rebootButton;
        [SerializeField] private TMP_Text _counters;

        private Button _repeatPopupButton;
        private Button _repeatSceneButton;
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

            // 반복 계측 버튼은 기존 버튼을 복제해 붙인다. 씬과 프리팹을 다시 만들지 않아도 된다. 리스너를 달기 전에 복제한다.
            _repeatPopupButton = CloneButton(_rebootButton, "RepeatPopup");
            _repeatSceneButton = CloneButton(_rebootButton, "RepeatScene");
            _repeatPopupButton.onClick.AddListener(RepeatRunner.RunPopupCycles);
            _repeatSceneButton.onClick.AddListener(RepeatRunner.RunSceneCycles);

            _toggleButton.onClick.AddListener(() => _body.SetActive(!_body.activeSelf));
            _delayButton.onClick.AddListener(LoadFaultSettings.CycleDelay);
            _delayOnceButton.onClick.AddListener(() => LoadFaultSettings.DelayOnce = !LoadFaultSettings.DelayOnce);
            _failButton.onClick.AddListener(LoadFaultSettings.CycleFailMode);
            _ignoreCancelButton.onClick.AddListener(() => LoadFaultSettings.IgnoreCancellation = !LoadFaultSettings.IgnoreCancellation);
            _patchModeButton.onClick.AddListener(PatchSimulationSettings.CycleMode);
            _patchFailButton.onClick.AddListener(() => PatchSimulationSettings.FailDownloadOnce = !PatchSimulationSettings.FailDownloadOnce);
            _clearCacheButton.onClick.AddListener(ClearCache);
            _contentBadgeButton.onClick.AddListener(CycleContentBadge);
            _rebootButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Boot));
        }

        /// <summary>
        /// 다음 실행부터 받을 CCD 콘텐츠 버전(v1 → v2 → latest)을 바꾼다. 설치한 기기에서 겨울(v1)에서 봄(v2)으로 바뀌는 패치를 보는 데 쓴다.
        /// Addressables 는 시작할 때 주소를 정하므로 "Boot 다시 실행"이 아니라 앱을 다시 시작해야 적용된다.
        /// </summary>
        private static void CycleContentBadge()
        {
            var badges = CcdLocation.TestBadges;
            var next = badges[(System.Array.IndexOf(badges, CcdLocation.TestBadge) + 1) % badges.Length];
            CcdLocation.TestBadge = next;
            LobbyLog.Info($"CCD 콘텐츠 {next} 선택(현재 실행은 {CcdLocation.Badge})");
            MessageToast.Show(next == CcdLocation.Badge ? $"CCD 콘텐츠 {next}" : $"앱을 다시 시작하면 CCD 콘텐츠 {next}를 받습니다.");
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
            SetLabel(_contentBadgeButton, CcdLocation.TestBadge == CcdLocation.Badge
                ? $"CCD 콘텐츠: {CcdLocation.Badge}"
                : $"CCD 콘텐츠: {CcdLocation.Badge} → {CcdLocation.TestBadge} (재시작 후)");
            SetLabel(_rebootButton, "Boot 다시 실행");
            SetLabel(_repeatPopupButton, $"반복 {RepeatRunner.DefaultCount}회: 상세 팝업 열고 닫기");
            SetLabel(_repeatSceneButton, $"반복 {RepeatRunner.DefaultCount}회: 챕터 1 왕복");
            _repeatPopupButton.interactable = !RepeatRunner.IsRunning;
            _repeatSceneButton.interactable = !RepeatRunner.IsRunning;

            // 같은 값이면 TMP_Text 에 다시 쓰지 않아 매 프레임 메시 재생성을 막는다.
            var text = $"보유 리소스 {ResourceCounters.LiveAssets} · 인스턴스 {ResourceCounters.LiveInstances}\n" +
                       $"누적 로드 {ResourceCounters.TotalAcquired} · 늦은 결과 반환 {ResourceCounters.StaleReleased}";
            if (!string.IsNullOrEmpty(RepeatRunner.Status))
                text += $"\n{RepeatRunner.Status}";
            if (text != _shown)
            {
                _shown = text;
                _counters.text = text;
            }
        }

        private static Button CloneButton(Button source, string name)
        {
            var copy = Instantiate(source, source.transform.parent, false);
            copy.name = name;
            copy.onClick.RemoveAllListeners();
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            return copy;
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text != text)
                label.text = text;
        }
    }
}
