using UnityEngine;

namespace LobbyFlow.Characters
{
    /// <summary>캐릭터 한 명의 표시 정보와 미리보기 리소스.</summary>
    [CreateAssetMenu(menuName = "Lobby Flow/Character Definition", fileName = "CharacterDefinition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _role;
        [SerializeField, TextArea(2, 4)] private string _description;
        [SerializeField] private Color _themeColor = Color.white;

        // Addressables 도입 전 임시 직접 참조다. 도입 시 AssetReference 로 바꾸고 로더만 교체한다.
        [SerializeField] private GameObject _previewPrefab;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Role => _role;
        public string Description => _description;
        public Color ThemeColor => _themeColor;
        public GameObject PreviewPrefab => _previewPrefab;
    }
}
