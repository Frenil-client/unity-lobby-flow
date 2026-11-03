using System;
using LobbyFlow.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>로비의 챕터 선택 버튼 하나. 아이콘은 로비 화면이 로드해 넣어 준다.</summary>
    public sealed class ChapterButtonView : MonoBehaviour
    {
        private static readonly Color NormalColor = new(0.20f, 0.23f, 0.30f);
        private static readonly Color SelectedColor = new(0.26f, 0.42f, 0.70f);

        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private GameObject _downloadTag;

        private Action _clicked;

        private void Awake() => _button.onClick.AddListener(() => _clicked?.Invoke());

        public void Bind(ChapterRecord chapter, Action clicked)
        {
            _clicked = clicked;
            _name.text = chapter.Name;
            SetDownloadRequired(false); // 받을 크기는 로비 화면이 확인해 알려 준다
            SetIcon(null);
        }

        public void SetDownloadRequired(bool required) => _downloadTag.SetActive(required);

        public void SetIcon(Sprite sprite)
        {
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
        }

        public void SetSelected(bool selected) => _background.color = selected ? SelectedColor : NormalColor;
    }
}
