using UnityEngine;

namespace LobbyFlow.Chapters
{
    /// <summary>던전 챕터 하나. 챕터 1은 로컬, 챕터 2·3은 원격 그룹으로 옮길 대상이다(SPEC 5.2).</summary>
    [CreateAssetMenu(menuName = "Lobby Flow/Chapter Definition", fileName = "ChapterDefinition")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _sceneName;
        [SerializeField] private Sprite _icon;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string SceneName => _sceneName;
        public Sprite Icon => _icon;
    }
}
