using System.Collections.Generic;
using UnityEngine;

namespace LobbyFlow.Characters
{
    /// <summary>로비에 표시할 캐릭터 목록. 표시 순서는 배열 순서를 따른다.</summary>
    [CreateAssetMenu(menuName = "Lobby Flow/Character Catalog", fileName = "CharacterCatalog")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        [SerializeField] private CharacterDefinition[] _characters = new CharacterDefinition[0];

        public IReadOnlyList<CharacterDefinition> Characters => _characters;
        public int Count => _characters.Length;

        public CharacterDefinition this[int index] => _characters[index];

        /// <summary>id 가 없거나 찾지 못하면 -1.</summary>
        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;

            for (var i = 0; i < _characters.Length; i++)
            {
                if (_characters[i] != null && _characters[i].Id == id)
                    return i;
            }

            return -1;
        }
    }
}
