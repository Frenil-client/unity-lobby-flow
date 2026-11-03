using System;
using System.Collections.Generic;
using LobbyFlow.Characters;
using LobbyFlow.Scenes;
using UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>로비 주 화면. 캐릭터 목록에서 상세 팝업을 열고, 선택한 캐릭터로 던전에 들어간다.</summary>
    public sealed class LobbyScreen : UIScreen
    {
        [SerializeField] private CharacterCatalog _catalog;
        [SerializeField] private RectTransform _slotContainer;
        [SerializeField] private CharacterSlotView _slotTemplate;
        [SerializeField] private Text _selectedLabel;
        [SerializeField] private Button _enterDungeonButton;

        private readonly List<CharacterSlotView> _slots = new();
        private bool _openingDetail;

        private void Awake()
        {
            _slotTemplate.gameObject.SetActive(false);

            for (var i = 0; i < _catalog.Count; i++)
            {
                var index = i;
                var slot = Instantiate(_slotTemplate, _slotContainer, false);
                slot.gameObject.SetActive(true);
                slot.Bind(_catalog[i], () => OpenDetail(index));
                _slots.Add(slot);
            }

            _enterDungeonButton.onClick.AddListener(() => SceneFlow.Load(SceneFlow.Dungeon));
            RefreshSelection();
        }

        private void Update()
        {
            // 열린 팝업이 있으면 그것을 닫고, 화면만 남았으면 아무것도 하지 않는다.
            if (BackKey.WasPressedThisFrame())
                UIManager.Instance?.OnBackPressed();
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

            popup.Bind(_catalog, index);
            await popup.WaitForCloseAsync();

            // 팝업이 열린 사이 씬을 떠났으면 이 화면은 이미 파괴됐다.
            if (this != null)
                RefreshSelection();
        }

        private void RefreshSelection()
        {
            var selected = Mathf.Max(0, _catalog.IndexOf(LobbyServices.SelectedCharacterId));
            for (var i = 0; i < _slots.Count; i++)
                _slots[i].SetSelected(i == selected);

            _selectedLabel.text = _catalog.Count > 0 ? $"출전 캐릭터: {_catalog[selected].DisplayName}" : "캐릭터 없음";
        }
    }
}
