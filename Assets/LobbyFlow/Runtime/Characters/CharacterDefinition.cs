using System.Collections.Generic;
using UnityEngine;

namespace LobbyFlow.Characters
{
    /// <summary>캐릭터 한 명의 표시 정보, 기본 미리보기 리소스, 코스튬 목록.</summary>
    [CreateAssetMenu(menuName = "Lobby Flow/Character Definition", fileName = "CharacterDefinition")]
    public sealed class CharacterDefinition : ScriptableObject, IPreviewSource
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _role;
        [SerializeField, TextArea(2, 4)] private string _description;
        [SerializeField] private Color _themeColor = Color.white;
        [SerializeField] private Sprite _portrait;
        [SerializeField] private GameObject _previewPrefab;
        [SerializeField] private CostumeDefinition[] _costumes = new CostumeDefinition[0];

        public string Id => _id;
        public string DisplayName => _displayName;
        public string Role => _role;
        public string Description => _description;
        public Color ThemeColor => _themeColor;
        public Sprite Portrait => _portrait;
        public IReadOnlyList<CostumeDefinition> Costumes => _costumes;

        public string PreviewKey => _id;
        public GameObject PreviewPrefab => _previewPrefab;

        /// <summary>id 가 비었거나 찾지 못하면 null. null 은 기본 모습을 뜻한다.</summary>
        public CostumeDefinition FindCostume(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var costume in _costumes)
            {
                if (costume != null && costume.Id == id)
                    return costume;
            }

            return null;
        }

        /// <summary>코스튬을 입었으면 코스튬, 아니면 기본 모습.</summary>
        public IPreviewSource ResolvePreview(string costumeId) => (IPreviewSource)FindCostume(costumeId) ?? this;
    }
}
