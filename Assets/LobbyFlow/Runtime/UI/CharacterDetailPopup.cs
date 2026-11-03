using LobbyFlow.Characters;
using LobbyFlow.Preview;
using LobbyFlow.Scenes;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 캐릭터 상세 팝업. 좌우 버튼으로 캐릭터를 바꾸고 미리보기를 로드한다.
    /// 미리보기 무대와 로드 요청은 팝업이 열려 있는 동안만 산다. 닫히거나(씬 언로드 포함) 파괴되면 함께 정리한다.
    /// </summary>
    public sealed class CharacterDetailPopup : UIPopup
    {
        private const int PreviewTextureSize = 512;
        private static readonly Vector3 StagePosition = new(0f, -500f, 0f);

        [SerializeField] private Text _name;
        [SerializeField] private Text _role;
        [SerializeField] private Text _description;
        [SerializeField] private Image _themeBar;
        [SerializeField] private RawImage _preview;
        [SerializeField] private GameObject _loadingGroup;
        [SerializeField] private RectTransform _spinner;
        [SerializeField] private GameObject _errorGroup;
        [SerializeField] private Text _errorText;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _selectButton;
        [SerializeField] private Button _departButton;

        private CharacterCatalog _catalog;
        private int _index;
        private PreviewStage _stage;
        private CharacterPreviewPresenter _presenter;

        private void Awake()
        {
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

        /// <summary>열린 직후 한 번 부른다. 표시할 캐릭터 목록과 시작 위치를 받는다.</summary>
        public void Bind(CharacterCatalog catalog, int index)
        {
            if (!IsOpen || catalog == null || catalog.Count == 0)
                return;

            _catalog = catalog;
            _index = Mathf.Clamp(index, 0, catalog.Count - 1);
            EnsurePreview();
            ShowCurrent();
        }

        protected override void OnClosing(UIResult result) => Teardown();

        private void OnDestroy() => Teardown();

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
            if (_catalog == null)
                return;

            _index = (_index + delta + _catalog.Count) % _catalog.Count;
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            var character = _catalog[_index];
            _name.text = character.DisplayName;
            _role.text = character.Role;
            _description.text = character.Description;
            _themeBar.color = character.ThemeColor;
            _presenter.Show(character);
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

        private void SelectAndClose()
        {
            if (_catalog == null)
                return;

            LobbyServices.SelectedCharacterId = _catalog[_index].Id;
            CloseConfirmed();
        }

        /// <summary>팝업을 닫지 않고 바로 씬을 떠난다. 미리보기 로딩 중 씬 이탈 경로를 그대로 탄다.</summary>
        private void SelectAndDepart()
        {
            if (_catalog == null)
                return;

            LobbyServices.SelectedCharacterId = _catalog[_index].Id;
            SceneFlow.Load(SceneFlow.Dungeon);
        }
    }
}
