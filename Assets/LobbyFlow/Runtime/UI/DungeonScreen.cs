using LobbyFlow.Content;
using LobbyFlow.Loading;
using LobbyFlow.Preview;
using LobbyFlow.Scenes;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 챕터 던전의 주 화면. 선택한 캐릭터(코스튬 포함)를 스폰 위치에 로드하고 로비로 돌아가는 길만 있다. 전투는 없다.
    /// 로드 요청은 이 화면이 소유하므로, 로딩 중 로비로 돌아가면 화면 파괴와 함께 요청이 정리된다.
    /// </summary>
    public sealed class DungeonScreen : UIScreen
    {
        [SerializeField] private string _chapterId;
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
            _retryButton.gameObject.SetActive(false);
            _status.text = string.Empty;
        }

        private async void Start()
        {
            ContentTables content;
            try
            {
                content = await LobbyServices.EnsureContentAsync();
            }
            catch (ContentDataException e)
            {
                LobbyLog.Error($"던전 데이터 오류: {e.Message}");
                _status.text = "게임 데이터를 읽지 못했습니다.";
                return;
            }

            // 기다리는 동안 씬을 떠났으면 이 화면은 이미 파괴됐다.
            if (this == null)
                return;

            var chapter = content.FindChapter(_chapterId);
            var chapterName = chapter != null ? chapter.Name : "던전";
            var character = content.FindCharacter(LobbyServices.SelectedCharacterId) ?? content.Characters[0];
            var costume = content.FindCostume(LobbyServices.SelectedCostumeId);
            if (costume != null && costume.CharacterId != character.Id)
                costume = null;

            _title.text = costume != null
                ? $"{chapterName} · {character.Name} ({costume.Name})"
                : $"{chapterName} · {character.Name}";

            _presenter = new CharacterPreviewPresenter(LobbyServices.PreviewLoader, _spawnPoint, "Dungeon");
            _presenter.StateChanged += RefreshState;
            _presenter.Show(costume != null ? costume : (IPreviewSource)character);
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
