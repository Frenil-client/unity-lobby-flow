using System;
using System.Threading;
using UnityEngine;

namespace LobbyFlow.Patching
{
    public readonly struct PatchCheckResult
    {
        public PatchCheckResult(long downloadBytes) => DownloadBytes = downloadBytes;

        /// <summary>시작 시 받아야 할 남은 크기. 이미 받은 번들은 빠진다.</summary>
        public long DownloadBytes { get; }

        public bool HasUpdate => DownloadBytes > 0;
    }

    public readonly struct DownloadProgress
    {
        public DownloadProgress(long downloadedBytes, long totalBytes)
        {
            DownloadedBytes = downloadedBytes;
            TotalBytes = totalBytes;
        }

        public long DownloadedBytes { get; }
        public long TotalBytes { get; }

        /// <summary>받은 바이트 기준 비율. 작업 진행률은 단계 비중이 섞여 막대가 튈 수 있어 쓰지 않는다(SPEC 6.3).</summary>
        public float Ratio => TotalBytes > 0 ? Mathf.Clamp01((float)DownloadedBytes / TotalBytes) : 1f;
    }

    /// <summary>서버에 닿지 못했거나 다운로드가 끊겼다. 재시도 대상이다.</summary>
    public sealed class PatchNetworkException : Exception
    {
        public PatchNetworkException(string message, bool injected = false) : base(message) => IsInjected = injected;

        /// <summary>모의 서비스가 만든 실패인지. 결과 기록에서 실제 네트워크 장애와 구분한다.</summary>
        public bool IsInjected { get; }
    }

    /// <summary>
    /// 시작 시 카탈로그 확인과 패치 다운로드. Boot 시퀀스만 쓴다.
    /// Addressables 도입 전에는 <see cref="SimulatedPatchService"/> 로 화면 흐름을 검증한다.
    /// </summary>
    public interface IPatchService
    {
        /// <summary>이전에 받은 카탈로그와 콘텐츠가 있어 오프라인으로도 시작할 수 있는지(SPEC 6.4).</summary>
        bool HasCachedContent { get; }

        /// <summary>원격 카탈로그 변경을 확인하고 받을 크기를 계산한다. 서버에 닿지 못하면 <see cref="PatchNetworkException"/>.</summary>
        Awaitable<PatchCheckResult> CheckAsync(CancellationToken cancellationToken);

        /// <summary>남은 패치를 받는다. 끊기면 <see cref="PatchNetworkException"/> 이고, 끝까지 받은 부분은 다음 시도에서 다시 받지 않는다.</summary>
        Awaitable DownloadAsync(Action<DownloadProgress> progress, CancellationToken cancellationToken);

        /// <summary>받은 콘텐츠를 지워 첫 실행 상태로 되돌린다. 테스트 메뉴가 쓴다.</summary>
        void ClearCache();
    }
}
