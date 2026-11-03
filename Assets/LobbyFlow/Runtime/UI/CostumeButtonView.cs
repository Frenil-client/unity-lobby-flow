using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LobbyFlow.UI
{
    /// <summary>상세 팝업의 코스튬 선택 버튼 하나. 기본 모습도 같은 버튼을 쓴다.</summary>
    public sealed class CostumeButtonView : MonoBehaviour
    {
        private static readonly Color NormalColor = new(0.30f, 0.33f, 0.40f);
        private static readonly Color SelectedColor = new(0.26f, 0.42f, 0.70f);

        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private GameObject _seasonBadge;

        private Action _clicked;

        private void Awake() => _button.onClick.AddListener(() => _clicked?.Invoke());

        public void Bind(string label, bool seasonal, Action clicked)
        {
            _clicked = clicked;
            _label.text = label;
            _seasonBadge.SetActive(seasonal);
        }

        public void SetSelected(bool selected) => _background.color = selected ? SelectedColor : NormalColor;
    }
}
