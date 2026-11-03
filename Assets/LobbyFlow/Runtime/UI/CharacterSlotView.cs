using System;
using LobbyFlow.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>로비 목록의 캐릭터 한 칸. 초상과 표시 정보만 들고 미리보기 리소스를 로드하지 않는다.</summary>
    public sealed class CharacterSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _swatch;
        [SerializeField] private Image _portrait;
        [SerializeField] private Text _name;
        [SerializeField] private Text _role;
        [SerializeField] private GameObject _selectedMark;

        private Action _clicked;

        private void Awake() => _button.onClick.AddListener(() => _clicked?.Invoke());

        public void Bind(CharacterDefinition character, Action clicked)
        {
            _clicked = clicked;
            _swatch.color = character.ThemeColor;
            _portrait.sprite = character.Portrait;
            _portrait.enabled = character.Portrait != null;
            _name.text = character.DisplayName;
            _role.text = character.Role;
        }

        public void SetSelected(bool selected) => _selectedMark.SetActive(selected);
    }
}
