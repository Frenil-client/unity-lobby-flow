using System;
using System.Threading;
using LobbyFlow.Characters;
using UnityEngine;

namespace LobbyFlow.Loading
{
    /// <summary>
    /// 실제 로더를 감싸 완료를 늦추거나 실패시킨다. 개발 빌드에서만 끼운다.
    /// 리소스를 먼저 받은 뒤 지연하므로, 지연 중 취소되거나 실패하면 받은 리소스를 여기서 반환하는 경로를 탄다.
    /// </summary>
    public sealed class FaultInjectingPreviewLoader : IPreviewAssetLoader
    {
        private readonly IPreviewAssetLoader _inner;

        public FaultInjectingPreviewLoader(IPreviewAssetLoader inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Awaitable<PreviewAsset> LoadAsync(CharacterDefinition character, CancellationToken cancellationToken)
        {
            var delay = LoadFaultSettings.ConsumeDelay();
            var fail = LoadFaultSettings.ConsumeFailure();
            var waitToken = LoadFaultSettings.IgnoreCancellation ? CancellationToken.None : cancellationToken;

            var asset = await _inner.LoadAsync(character, cancellationToken);

            try
            {
                if (delay > 0f)
                    await Awaitable.WaitForSecondsAsync(delay, waitToken);

                if (fail)
                    throw new PreviewLoadException(character.Id, "테스트 메뉴가 주입한 실패", injected: true);

                return asset;
            }
            catch
            {
                asset.Release();
                throw;
            }
        }
    }
}
