using System;
using System.Threading;
using UnityEngine;

namespace LobbyFlow.Patching
{
    /// <summary>
    /// Addressables 도입 전 Boot 화면 흐름 검증용 모의 패치. 실제 네트워크를 쓰지 않는다.
    /// 받은 양은 앱이 켜져 있는 동안 유지해, 끊긴 뒤 재시도가 이어 받는 동작을 흉내 낸다.
    /// </summary>
    public sealed class SimulatedPatchService : IPatchService
    {
        private const float CheckSeconds = 0.6f;

        private long _downloadedBytes;
        private bool _completed;

        public bool HasCachedContent => PatchSimulationSettings.Mode != PatchSimulationMode.OfflineFirstRun;

        public async Awaitable<PatchCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            await Awaitable.WaitForSecondsAsync(CheckSeconds, cancellationToken);

            switch (PatchSimulationSettings.Mode)
            {
                case PatchSimulationMode.OfflineWithCache:
                case PatchSimulationMode.OfflineFirstRun:
                    throw new PatchNetworkException("모의: 서버에 연결할 수 없다", injected: true);
                case PatchSimulationMode.UpToDate:
                    return new PatchCheckResult(0);
                default:
                    return new PatchCheckResult(_completed ? 0 : PatchSimulationSettings.PatchBytes - _downloadedBytes);
            }
        }

        public async Awaitable DownloadAsync(Action<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            var total = PatchSimulationSettings.PatchBytes;
            var failAt = PatchSimulationSettings.FailDownloadOnce ? total / 2 : long.MaxValue;
            progress?.Invoke(new DownloadProgress(_downloadedBytes, total));

            while (_downloadedBytes < total)
            {
                await Awaitable.NextFrameAsync(cancellationToken);

                var step = (long)(PatchSimulationSettings.BytesPerSecond * Time.unscaledDeltaTime);
                _downloadedBytes = Math.Min(total, _downloadedBytes + Math.Max(1, step));

                if (_downloadedBytes >= failAt)
                {
                    PatchSimulationSettings.FailDownloadOnce = false;
                    throw new PatchNetworkException("모의: 다운로드가 끊겼다", injected: true);
                }

                progress?.Invoke(new DownloadProgress(_downloadedBytes, total));
            }

            _completed = true;
        }

        public void ClearCache()
        {
            _downloadedBytes = 0;
            _completed = false;
        }
    }
}
