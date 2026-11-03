using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LobbyFlow.Loading
{
    /// <summary>
    /// Addressables 로 미리보기 프리팹을 로드한다. 반환은 돌려준 <see cref="PreviewAsset"/> 의 Release 한 곳에서만 한다.
    /// Addressables 작업은 중간에 멈출 수 없으므로, 취소 요청을 받아도 완료를 기다린 뒤 핸들을 반환하고 취소 예외를 던진다(SPEC 4.2).
    /// </summary>
    public sealed class AddressablesPreviewAssetLoader : IPreviewAssetLoader
    {
        public async Awaitable<PreviewAsset> LoadAsync(IPreviewSource source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handle = Addressables.LoadAssetAsync<GameObject>(source.PreviewAddress);
            ResourceCounters.AssetAcquired();

            while (!handle.IsDone)
                await Awaitable.NextFrameAsync();

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                var reason = handle.OperationException?.Message ?? "알 수 없는 오류";
                Release(handle);
                throw new PreviewLoadException(source.PreviewKey, reason);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Release(handle);
                throw new OperationCanceledException(cancellationToken);
            }

            return new PreviewAsset(source.PreviewKey, handle.Result, () => Release(handle));
        }

        private static void Release(AsyncOperationHandle<GameObject> handle)
        {
            Addressables.Release(handle);
            ResourceCounters.AssetReleased();
        }
    }
}
