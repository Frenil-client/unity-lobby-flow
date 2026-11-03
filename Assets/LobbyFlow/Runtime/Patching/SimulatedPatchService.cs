using System;
using System.Collections.Generic;
using System.Threading;
using LobbyFlow.Content;
using UnityEngine;

namespace LobbyFlow.Patching
{
    /// <summary>
    /// 화면 흐름 검증용 모의 패치. 실제 네트워크를 쓰지 않는다. 오프라인·끊김처럼 실제로 만들기 번거로운 경로를 재현한다.
    /// 라벨마다 받은 양을 앱이 켜져 있는 동안 유지해, 끊긴 뒤 재시도가 이어 받는 동작을 흉내 낸다.
    /// </summary>
    public sealed class SimulatedPatchService : IPatchService
    {
        private const float CheckSeconds = 0.6f;

        private readonly Dictionary<string, long> _downloaded = new();

        public async Awaitable<bool> HasCachedContentAsync(CancellationToken cancellationToken)
        {
            await Awaitable.NextFrameAsync(cancellationToken);
            return PatchSimulationSettings.Mode != PatchSimulationMode.OfflineFirstRun;
        }

        public async Awaitable<PatchCheckResult> CheckAsync(CancellationToken cancellationToken)
        {
            await Awaitable.WaitForSecondsAsync(CheckSeconds, cancellationToken);
            ThrowIfOffline();

            if (PatchSimulationSettings.Mode == PatchSimulationMode.UpToDate)
                return new PatchCheckResult(0, false);

            return new PatchCheckResult(Remaining(ContentAddresses.PatchBootLabel), true);
        }

        public async Awaitable<long> GetDownloadSizeAsync(string label, CancellationToken cancellationToken)
        {
            await Awaitable.NextFrameAsync(cancellationToken);
            ThrowIfOffline();
            return PatchSimulationSettings.Mode == PatchSimulationMode.UpToDate ? 0 : Remaining(label);
        }

        public async Awaitable DownloadAsync(string label, Action<DownloadProgress> progress, CancellationToken cancellationToken)
        {
            var total = TotalBytes(label);
            var failAt = PatchSimulationSettings.FailDownloadOnce ? total / 2 : long.MaxValue;
            _downloaded.TryGetValue(label, out var done);
            progress?.Invoke(new DownloadProgress(done, total));

            while (done < total)
            {
                await Awaitable.NextFrameAsync(cancellationToken);

                var step = (long)(PatchSimulationSettings.BytesPerSecond * Time.unscaledDeltaTime);
                done = Math.Min(total, done + Math.Max(1, step));
                _downloaded[label] = done;

                if (done >= failAt)
                {
                    PatchSimulationSettings.FailDownloadOnce = false;
                    throw new PatchNetworkException("모의: 다운로드가 끊겼다", injected: true);
                }

                progress?.Invoke(new DownloadProgress(done, total));
            }
        }

        public void ClearCache(IEnumerable<string> labels)
        {
            foreach (var label in labels)
                _downloaded.Remove(label);
        }

        private static void ThrowIfOffline()
        {
            if (PatchSimulationSettings.Mode is PatchSimulationMode.OfflineWithCache or PatchSimulationMode.OfflineFirstRun)
                throw new PatchNetworkException("모의: 서버에 연결할 수 없다", injected: true);
        }

        private static long TotalBytes(string label) =>
            label == ContentAddresses.PatchBootLabel ? PatchSimulationSettings.PatchBytes : PatchSimulationSettings.ChapterBytes;

        private long Remaining(string label)
        {
            _downloaded.TryGetValue(label, out var done);
            return TotalBytes(label) - done;
        }
    }
}
