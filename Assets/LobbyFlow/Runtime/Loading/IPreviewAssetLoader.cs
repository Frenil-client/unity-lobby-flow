using System.Threading;
using LobbyFlow.Characters;
using UnityEngine;

namespace LobbyFlow.Loading
{
    /// <summary>
    /// 미리보기 리소스 로더.
    /// 정상 완료하면 돌려준 <see cref="PreviewAsset"/> 의 해제 책임이 호출한 쪽으로 넘어간다.
    /// 취소나 실패로 예외를 던질 때는 그때까지 확보한 리소스를 로더가 반환한다.
    /// 취소를 요청해도 실제 작업이 끝나 결과를 돌려줄 수 있으므로, 호출한 쪽은 결과를 받은 뒤에도 유효한 요청인지 확인한다.
    /// </summary>
    public interface IPreviewAssetLoader
    {
        Awaitable<PreviewAsset> LoadAsync(CharacterDefinition character, CancellationToken cancellationToken);
    }
}
