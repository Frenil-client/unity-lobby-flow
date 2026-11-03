using System;
using System.Collections.Generic;
using LobbyFlow.Audio;
using LobbyFlow.Content;
using LobbyFlow.Loading;
using LobbyFlow.Patching;
using LobbyFlow.Scenes;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>
    /// 로비 주 화면. 캐릭터 목록에서 상세 팝업을 열고, 고른 챕터에 선택한 캐릭터로 들어간다.
    /// 초상·챕터 아이콘·BGM·이벤트 배너는 이 화면이 Addressables 로 로드해 소유하고, 화면이 사라질 때 반환한다(SPEC 7.2).
    /// </summary>
    public sealed class LobbyScreen : UIScreen
    {
        [SerializeField] private RectTransform _slotContainer;
        [SerializeField] private CharacterSlotView _slotTemplate;
        [SerializeField] private RectTransform _chapterContainer;
        [SerializeField] private ChapterButtonView _chapterTemplate;
        [SerializeField] private Text _selectedLabel;
        [SerializeField] private Button _enterDungeonButton;
        [SerializeField] private GameObject _eventGroup;
        [SerializeField] private Image _eventBanner;
        [SerializeField] private Text _eventTitle;
        [SerializeField] private Text _eventBody;

        private readonly List<CharacterSlotView> _slots = new();
        private readonly List<ChapterButtonView> _chapterButtons = new();
        private AssetHandleSet _assets;
        private ContentTables _content;
        private AudioClip _bgm;
        private bool _openingDetail;

        private void Awake()
        {
            _assets = new AssetHandleSet("Lobby");
            _slotTemplate.gameObject.SetActive(false);
            _chapterTemplate.gameObject.SetActive(false);
            _eventGroup.SetActive(false);
            _selectedLabel.text = string.Empty;
            _enterDungeonButton.onClick.AddListener(EnterChapter);
        }

        private async void Start()
        {
            MessageToast.Show(LobbyServices.ConsumeNotice());

            try
            {
                _content = await LobbyServices.EnsureContentAsync();
            }
            catch (ContentDataException e)
            {
                LobbyLog.Error($"로비 데이터 오류: {e.Message}");
                MessageToast.Show("게임 데이터를 읽지 못했습니다.");
                return;
            }

            // 기다리는 동안 씬을 떠났으면 이 화면은 이미 파괴됐다.
            if (this == null)
                return;

            BuildSlots();
            BuildChapters();
            ShowEventNotice();
            RefreshSelection();
            LoadBgm();
        }

        private void OnDestroy()
        {
            ChapterDownload.Downloaded -= OnChapterDownloaded;
            AudioService.StopBgm(_bgm);
            _assets.Dispose();
        }

        private void Update()
        {
            // 열린 팝업이 있으면 그것을 닫고, 화면만 남았으면 아무것도 하지 않는다.
            if (BackKey.WasPressedThisFrame())
                UIManager.Instance?.OnBackPressed();
        }

        private void BuildSlots()
        {
            for (var i = 0; i < _content.Characters.Count; i++)
            {
                var index = i;
                var character = _content.Characters[i];
                var slot = Instantiate(_slotTemplate, _slotContainer, false);
                slot.gameObject.SetActive(true);
                slot.Bind(character, () => OpenDetail(index));
                _slots.Add(slot);
                LoadInto<Sprite>(character.PortraitAddress, slot.SetPortrait);
            }
        }

        private void BuildChapters()
        {
            foreach (var chapter in _content.Chapters)
            {
                var button = Instantiate(_chapterTemplate, _chapterContainer, false);
                button.gameObject.SetActive(true);
                button.Bind(chapter, () => SelectChapter(chapter));
                _chapterButtons.Add(button);
                LoadInto<Sprite>(chapter.IconAddress, button.SetIcon);
            }

            ChapterDownload.Downloaded += OnChapterDownloaded;
            RefreshDownloadTags();
        }

        /// <summary>
        /// 원격 챕터마다 남은 받을 크기를 확인해, 받을 것이 있을 때만 추가 다운로드 표시를 켠다.
        /// 크기를 확인하지 못하면 받아야 하는 것으로 보고 표시를 남긴다.
        /// </summary>
        private async void RefreshDownloadTags()
        {
            var cancellationToken = destroyCancellationToken;
            for (var i = 0; i < _content.Chapters.Count; i++)
            {
                var chapter = _content.Chapters[i];
                if (!chapter.RequiresDownload)
                    continue;

                bool required;
                try
                {
                    required = await LobbyServices.PatchService.GetDownloadSizeAsync(chapter.DownloadLabel, cancellationToken) > 0;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (PatchNetworkException e)
                {
                    LobbyLog.Warning($"챕터 받을 크기 확인 실패 {chapter.Id}: {e.Message}");
                    required = true;
                }

                _chapterButtons[i].SetDownloadRequired(required);
            }
        }

        private void OnChapterDownloaded(ChapterRecord chapter)
        {
            for (var i = 0; i < _chapterButtons.Count; i++)
            {
                if (_content.Chapters[i] == chapter)
                    _chapterButtons[i].SetDownloadRequired(false);
            }
        }

        private void ShowEventNotice()
        {
            var notice = _content.Notice;
            if (notice.IsEmpty)
                return;

            _eventGroup.SetActive(true);
            _eventTitle.text = notice.Title;
            _eventBody.text = notice.Body;
            LoadInto<Sprite>(ContentAddresses.EventBanner, sprite =>
            {
                _eventBanner.sprite = sprite;
                _eventBanner.enabled = sprite != null;
            });
        }

        private async void LoadBgm()
        {
            var clip = await _assets.LoadAsync<AudioClip>(ContentAddresses.LobbyBgm);
            if (clip == null || this == null)
                return;

            _bgm = clip;
            AudioService.PlayBgm(clip);
        }

        /// <summary>리소스를 로드해 넣는다. 화면이 먼저 사라지면 결과는 null 이 되고 넣지 않는다.</summary>
        private async void LoadInto<T>(string address, Action<T> apply) where T : UnityEngine.Object
        {
            var asset = await _assets.LoadAsync<T>(address);
            if (asset != null && this != null)
                apply(asset);
        }

        private async void OpenDetail(int index)
        {
            var manager = UIManager.Instance;
            if (manager == null || _openingDetail || manager.IsOpen<CharacterDetailPopup>())
                return;

            // 프리팹 조회가 끝나기 전 연타로 팝업이 둘 열리지 않게 한다.
            _openingDetail = true;
            CharacterDetailPopup popup;
            try
            {
                popup = await manager.OpenAsync<CharacterDetailPopup>(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _openingDetail = false;
            }

            if (popup == null)
                return;

            popup.Bind(_content, index, EnterChapter);
            await popup.WaitForCloseAsync();

            // 팝업이 열린 사이 씬을 떠났으면 이 화면은 이미 파괴됐다.
            if (this != null)
                RefreshSelection();
        }

        private void SelectChapter(ChapterRecord chapter)
        {
            LobbyServices.SelectedChapterId = chapter.Id;
            RefreshSelection();
        }

        private void EnterChapter()
        {
            if (_content == null)
                return;

            SceneFlow.EnterChapter(SelectedChapter());
        }

        private ChapterRecord SelectedChapter() => _content.FindChapter(LobbyServices.SelectedChapterId) ?? _content.Chapters[0];

        private CharacterRecord SelectedCharacter() => _content.FindCharacter(LobbyServices.SelectedCharacterId) ?? _content.Characters[0];

        private void RefreshSelection()
        {
            if (_content == null)
                return;

            var character = SelectedCharacter();
            for (var i = 0; i < _slots.Count; i++)
                _slots[i].SetSelected(_content.Characters[i] == character);

            var chapter = SelectedChapter();
            for (var i = 0; i < _chapterButtons.Count; i++)
                _chapterButtons[i].SetSelected(_content.Chapters[i] == chapter);

            var costume = _content.FindCostume(LobbyServices.SelectedCostumeId);
            _selectedLabel.text = costume != null && costume.CharacterId == character.Id
                ? $"출전: {character.Name} ({costume.Name})"
                : $"출전: {character.Name}";
        }
    }
}
