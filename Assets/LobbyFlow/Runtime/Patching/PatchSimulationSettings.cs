using UnityEngine;

namespace LobbyFlow.Patching
{
    public enum PatchSimulationMode
    {
        UpToDate,
        UpdateAvailable,
        OfflineWithCache,
        OfflineFirstRun,
    }

    /// <summary>테스트 메뉴가 바꾸는 모의 패치 조건. 모의 결과는 실제 CCD 검증과 구분해 기록한다.</summary>
    public static class PatchSimulationSettings
    {
        public const long PatchBytes = 24L * 1024 * 1024;
        public const long BytesPerSecond = 6L * 1024 * 1024;

        public static PatchSimulationMode Mode { get; set; } = PatchSimulationMode.UpdateAvailable;

        /// <summary>켜면 다음 다운로드가 절반쯤에서 한 번 끊긴다.</summary>
        public static bool FailDownloadOnce { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Mode = PatchSimulationMode.UpdateAvailable;
            FailDownloadOnce = false;
        }

        public static void CycleMode() => Mode = (PatchSimulationMode)(((int)Mode + 1) % 4);
    }
}
