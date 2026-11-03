using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LobbyFlow.Audio
{
    /// <summary>버튼을 누를 때 효과음을 낸다. 비활성 버튼에서는 내지 않는다.</summary>
    [RequireComponent(typeof(Selectable))]
    public sealed class ClickSound : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private AudioClip _clip;

        private Selectable _selectable;

        private void Awake() => _selectable = GetComponent<Selectable>();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_selectable.IsInteractable())
                AudioService.PlaySfx(_clip);
        }
    }
}
