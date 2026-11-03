using System;
using LobbyFlow.Chapters;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>로비의 챕터 선택 버튼 하나.</summary>
    public sealed class ChapterButtonView : MonoBehaviour
    {
        private static readonly Color NormalColor = new(0.20f, 0.23f, 0.30f);
        private static readonly Color SelectedColor = new(0.26f, 0.42f, 0.70f);

        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _name;

        private Action _clicked;

        private void Awake() => _button.onClick.AddListener(() => _clicked?.Invoke());

        public void Bind(ChapterDefinition chapter, Action clicked)
        {
            _clicked = clicked;
            _icon.sprite = chapter.Icon;
            _name.text = chapter.DisplayName;
        }

        public void SetSelected(bool selected) => _background.color = selected ? SelectedColor : NormalColor;
    }
}
