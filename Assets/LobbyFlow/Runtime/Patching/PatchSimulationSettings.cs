using UnityEngine;

namespace LobbyFlow.Patching
{
    public enum PatchSimulationMode
    {
        /// <summary>모의하지 않고 실제 Addressables 서비스를 쓴다.</summary>
        Addressables,
        UpToDate,
        UpdateAvailable,
        OfflineWithCache,
        OfflineFirstRun,
    }

    /// <summary>테스트 메뉴가 바꾸는 모의 패치 조건. 모의 결과는 실제 원격 검증과 구분해 기록한다.</summary>
    public static class PatchSimulationSettings
    {
        public const long PatchBytes = 24L * 1024 * 1024;
        public const long ChapterBytes = 8L * 1024 * 1024;
        public const long BytesPerSecond = 6L * 1024 * 1024;

        private const int ModeCount = 5;

        public static PatchSimulationMode Mode { get; set; } = PatchSimulationMode.Addressables;

        public static bool IsSimulated => Mode != PatchSimulationMode.Addressables;

        /// <summary>켜면 다음 모의 다운로드가 절반쯤에서 한 번 끊긴다.</summary>
        public static bool FailDownloadOnce { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Mode = PatchSimulationMode.Addressables;
            FailDownloadOnce = false;
        }

        public static void CycleMode() => Mode = (PatchSimulationMode)(((int)Mode + 1) % ModeCount);
    }
}
