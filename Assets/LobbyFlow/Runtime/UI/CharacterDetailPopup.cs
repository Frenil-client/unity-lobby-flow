using System;
using System.Collections.Generic;
using LobbyFlow.Content;
using LobbyFlow.Loading;
using LobbyFlow.Preview;
using TMPro;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 캐릭터 상세 팝업. 좌우 버튼으로 캐릭터를, 코스튬 버튼으로 모습을 바꾸고 미리보기를 로드한다.
    /// 미리보기 무대와 로드 요청은 팝업이 열려 있는 동안만 산다. 닫히거나(씬 언로드 포함) 파괴되면 함께 정리한다.
    /// </summary>
    public sealed class CharacterDetailPopup : UIPopup
    {
        private const int PreviewTextureSize = 512;
        private const string BaseCostumeLabel = "기본";
        private static readonly Vector3 StagePosition = new(0f, -500f, 0f);

        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _role;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private Image _themeBar;
        [SerializeField] private RawImage _preview;
        [SerializeField] private GameObject _loadingGroup;
        [SerializeField] private RectTransform _spinner;
        [SerializeField] private GameObject _errorGroup;
        [SerializeField] private TMP_Text _errorText;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private RectTransform _costumeContainer;
        [SerializeField] private CostumeButtonView _costumeTemplate;
        [SerializeField] private Button _selectButton;
        [SerializeField] private Button _departButton;

        private readonly List<CostumeButtonView> _costumeButtons = new();
        private readonly List<CostumeRecord> _costumeButtonRecords = new(); // 기본 모습은 null
        private ContentTables _content;
        private Action _depart;
        private int _index;
        private CostumeRecord _costume;
        private PreviewStage _stage;
        private CharacterPreviewPresenter _presenter;

        private void Awake()
        {
            _costumeTemplate.gameObject.SetActive(false);
            _prevButton.onClick.AddListener(() => Step(-1));
            _nextButton.onClick.AddListener(() => Step(1));
            _retryButton.onClick.AddListener(() => _presenter?.Retry());
            _closeButton.onClick.AddListener(Close);
            _selectButton.onClick.AddListener(SelectAndClose);
            _departButton.onClick.AddListener(SelectAndDepart);
        }

        private void Update()
        {
            if (_loadingGroup.activeSelf)
                _spinner.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 열린 직후 한 번 부른다. 데이터와 시작 위치, 출발 버튼이 할 일을 받는다.
        /// 출발은 팝업을 닫지 않고 씬을 떠나므로 미리보기 로딩 중 씬 이탈 경로를 그대로 탄다.
        /// </summary>
        public void Bind(ContentTables content, int index, Action depart)
        {
            if (!IsOpen || content == null || content.Characters.Count == 0)
                return;

            _content = content;
            _depart = depart;
            _index = Mathf.Clamp(index, 0, content.Characters.Count - 1);
            EnsurePreview();
            ShowCharacter();
        }

        protected override void OnClosing(UIResult result) => Teardown();

        private void OnDestroy() => Teardown();

        private CharacterRecord CurrentCharacter => _content.Characters[_index];

        private void EnsurePreview()
        {
            if (_presenter != null)
                return;

            _stage = PreviewStage.Create("DetailPreviewStage", StagePosition, PreviewTextureSize, new Color(0.13f, 0.15f, 0.19f));
            _preview.texture = _stage.Texture;
            _presenter = new CharacterPreviewPresenter(LobbyServices.PreviewLoader, _stage.Anchor, "Detail");
            _presenter.StateChanged += RefreshState;
        }

        private void Teardown()
        {
            if (_presenter != null)
            {
                _presenter.Dispose();
                _presenter = null;
            }

            if (_stage != null)
                Destroy(_stage.gameObject);

            _stage = null;
            if (_preview != null)
                _preview.texture = null;
        }

        private void Step(int delta)
        {
            if (_content == null)
                return;

            var count = _content.Characters.Count;
            _index = (_index + delta + count) % count;
            ShowCharacter();
        }

        private void ShowCharacter()
        {
            var character = CurrentCharacter;
            _name.text = character.Name;
            _role.text = character.Role;
            _description.text = character.Description;
            _themeBar.color = character.ThemeColor;

            // 출전 중인 캐릭터면 입고 있는 코스튬으로, 아니면 기본 모습으로 시작한다.
            var selected = _content.FindCostume(LobbyServices.SelectedCostumeId);
            var startCostume = character.Id == LobbyServices.SelectedCharacterId && selected?.CharacterId == character.Id ? selected : null;

            RebuildCostumeButtons(character);
            ShowCostume(startCostume);
        }

        private void RebuildCostumeButtons(CharacterRecord character)
        {
            foreach (var button in _costumeButtons)
                Destroy(button.gameObject);
            _costumeButtons.Clear();
            _costumeButtonRecords.Clear();

            AddCostumeButton(BaseCostumeLabel, false, null);
            foreach (var costume in _content.CostumesOf(character.Id))
                AddCostumeButton(costume.Name, costume.Seasonal, costume);
        }

        private void AddCostumeButton(string label, bool seasonal, CostumeRecord costume)
        {
            var button = Instantiate(_costumeTemplate, _costumeContainer, false);
            button.gameObject.SetActive(true);
            button.Bind(label, seasonal, () => ShowCostume(costume));
            _costumeButtons.Add(button);
            _costumeButtonRecords.Add(costume);
        }

        private void ShowCostume(CostumeRecord costume)
        {
            _costume = costume;
            for (var i = 0; i < _costumeButtons.Count; i++)
                _costumeButtons[i].SetSelected(_costumeButtonRecords[i] == costume);

            _presenter.Show(costume != null ? costume : (IPreviewSource)CurrentCharacter);
        }

        private void RefreshState()
        {
            var state = _presenter.State;
            _loadingGroup.SetActive(state == PreviewState.Loading);
            _errorGroup.SetActive(state == PreviewState.Failed);
            _preview.enabled = state == PreviewState.Ready;

            if (state == PreviewState.Failed)
                _errorText.text = "미리보기를 불러오지 못했습니다.";
        }

        private void SaveSelection()
        {
            LobbyServices.SelectedCharacterId = CurrentCharacter.Id;
            LobbyServices.SelectedCostumeId = _costume?.Id;
        }

        private void SelectAndClose()
        {
            if (_content == null)
                return;

            SaveSelection();
            CloseConfirmed();
        }

        private void SelectAndDepart()
        {
            if (_content == null)
                return;

            SaveSelection();
            _depart?.Invoke();
        }
    }
}
