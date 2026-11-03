namespace LobbyFlow.Loading
{
    /// <summary>미리보기로 띄울 수 있는 대상. 기본 캐릭터와 코스튬이 같은 로딩 경로를 쓴다.</summary>
    public interface IPreviewSource
    {
        /// <summary>로그와 실패 보고에 쓰는 식별자.</summary>
        string PreviewKey { get; }

        /// <summary>미리보기 프리팹의 Addressables 주소.</summary>
        string PreviewAddress { get; }
    }
}
