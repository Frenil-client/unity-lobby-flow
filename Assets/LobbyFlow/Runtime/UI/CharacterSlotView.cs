using System;
using LobbyFlow.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>로비 목록의 캐릭터 한 칸. 초상은 로비 화면이 로드해 넣어 준다. 이 칸은 리소스를 소유하지 않는다.</summary>
    public sealed class CharacterSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _swatch;
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _role;
        [SerializeField] private GameObject _selectedMark;

        private Action _clicked;

        private void Awake() => _button.onClick.AddListener(() => _clicked?.Invoke());

        public void Bind(CharacterRecord character, Action clicked)
        {
            _clicked = clicked;
            _swatch.color = character.ThemeColor;
            _name.text = character.Name;
            _role.text = character.Role;
            SetPortrait(null);
        }

        public void SetPortrait(Sprite sprite)
        {
            _portrait.sprite = sprite;
            _portrait.enabled = sprite != null;
        }

        public void SetSelected(bool selected) => _selectedMark.SetActive(selected);
    }
}
