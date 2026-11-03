using UnityEngine;

namespace LobbyFlow.Characters
{
    /// <summary>캐릭터 하나에 딸린 코스튬. 시즌 코스튬은 원격 그룹으로 옮길 대상이다(SPEC 5.2).</summary>
    [CreateAssetMenu(menuName = "Lobby Flow/Costume Definition", fileName = "CostumeDefinition")]
    public sealed class CostumeDefinition : ScriptableObject, IPreviewSource
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private bool _isSeasonal;
        [SerializeField] private GameObject _previewPrefab;

        public string Id => _id;
        public string DisplayName => _displayName;
        public bool IsSeasonal => _isSeasonal;

        public string PreviewKey => _id;
        public GameObject PreviewPrefab => _previewPrefab;
    }
}
