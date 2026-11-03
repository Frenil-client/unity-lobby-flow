using System.Threading;
using LobbyFlow.Characters;
using UnityEngine;

namespace LobbyFlow.Loading
{
    /// <summary>
    /// Addressables 도입 전 임시 로더. 정의에 직접 연결된 프리팹을 다음 프레임에 돌려준다.
    /// 실제 읽기가 없으므로 로딩 지연과 실패는 <see cref="FaultInjectingPreviewLoader"/> 가 만든다.
    /// </summary>
    public sealed class DirectPreviewAssetLoader : IPreviewAssetLoader
    {
        public async Awaitable<PreviewAsset> LoadAsync(IPreviewSource source, CancellationToken cancellationToken)
        {
            // 실제 로더처럼 호출한 프레임 안에서 완료하지 않는다. 동기 완료를 전제로 한 코드가 숨지 않게 한다.
            await Awaitable.NextFrameAsync(cancellationToken);

            if (source.PreviewPrefab == null)
                throw new PreviewLoadException(source.PreviewKey, "미리보기 프리팹이 연결되지 않았다");

            ResourceCounters.AssetAcquired();
            return new PreviewAsset(source.PreviewKey, source.PreviewPrefab, ResourceCounters.AssetReleased);
        }
    }
}
