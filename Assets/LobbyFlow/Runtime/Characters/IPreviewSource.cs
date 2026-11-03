using UnityEngine;

namespace LobbyFlow.Characters
{
    /// <summary>미리보기로 띄울 수 있는 대상. 기본 캐릭터와 코스튬이 같은 로딩 경로를 쓴다.</summary>
    public interface IPreviewSource
    {
        /// <summary>로그와 실패 보고에 쓰는 식별자.</summary>
        string PreviewKey { get; }

        // Addressables 도입 전 임시 직접 참조다. 도입 시 AssetReference 로 바꾸고 로더만 교체한다.
        GameObject PreviewPrefab { get; }
    }
}
