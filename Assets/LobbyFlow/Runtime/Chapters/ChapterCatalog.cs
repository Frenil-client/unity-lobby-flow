using UnityEngine;

namespace LobbyFlow.Chapters
{
    /// <summary>로비에서 고를 수 있는 챕터 목록. 표시 순서는 배열 순서를 따른다.</summary>
    [CreateAssetMenu(menuName = "Lobby Flow/Chapter Catalog", fileName = "ChapterCatalog")]
    public sealed class ChapterCatalog : ScriptableObject
    {
        [SerializeField] private ChapterDefinition[] _chapters = new ChapterDefinition[0];

        public int Count => _chapters.Length;

        public ChapterDefinition this[int index] => _chapters[index];

        /// <summary>id 가 없거나 찾지 못하면 -1.</summary>
        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;

            for (var i = 0; i < _chapters.Length; i++)
            {
                if (_chapters[i] != null && _chapters[i].Id == id)
                    return i;
            }

            return -1;
        }

        /// <summary>현재 씬 이름으로 챕터를 찾는다. 던전 화면이 자기 챕터를 알아내는 데 쓴다.</summary>
        public ChapterDefinition FindByScene(string sceneName)
        {
            foreach (var chapter in _chapters)
            {
                if (chapter != null && chapter.SceneName == sceneName)
                    return chapter;
            }

            return null;
        }
    }
}
