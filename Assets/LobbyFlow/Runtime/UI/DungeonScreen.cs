using LobbyFlow.Characters;
using LobbyFlow.Preview;
using LobbyFlow.Scenes;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 던전 주 화면. 선택한 캐릭터를 스폰 위치에 로드하고 로비로 돌아가는 길만 있다. 전투는 없다.
    /// 로드 요청은 이 화면이 소유하므로, 로딩 중 로비로 돌아가면 화면 파괴와 함께 요청이 정리된다.
    /// </summary>
    public sealed class DungeonScreen : UIScreen
    {
        [SerializeField] private CharacterCatalog _catalog;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Text _title;
        [SerializeField] private Text _status;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _returnButton;

        private CharacterPreviewPresenter _presenter;

        private void Awake()
        {
            _returnButton.onClick.AddListener(ReturnToLobby);
            _retryButton.onClick.AddListener(() => _presenter?.Retry());

            if (_catalog.Count == 0)
            {
                _title.text = "던전";
                _status.text = "캐릭터 목록이 비어 있습니다.";
                _retryButton.gameObject.SetActive(false);
                return;
            }

            var character = _catalog[Mathf.Max(0, _catalog.IndexOf(LobbyServices.SelectedCharacterId))];
            _title.text = $"던전 · {character.DisplayName}";

            _presenter = new CharacterPreviewPresenter(LobbyServices.PreviewLoader, _spawnPoint, "Dungeon");
            _presenter.StateChanged += RefreshState;
            _presenter.Show(character);
        }

        private void Update()
        {
            if (BackKey.WasPressedThisFrame() && !(UIManager.Instance?.OnBackPressed() ?? false))
                ReturnToLobby();
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _presenter = null;
        }

        private void RefreshState()
        {
            var state = _presenter.State;
            _retryButton.gameObject.SetActive(state == PreviewState.Failed);
            _status.text = state switch
            {
                PreviewState.Loading => "캐릭터 불러오는 중…",
                PreviewState.Failed => "캐릭터를 불러오지 못했습니다.",
                _ => string.Empty,
            };
        }

        private static void ReturnToLobby() => SceneFlow.Load(SceneFlow.Lobby);
    }
}
