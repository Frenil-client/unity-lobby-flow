using UnityEngine;

namespace LobbyFlow.Loading
{
    /// <summary>
    /// 보유 중인 미리보기 리소스와 생성 인스턴스 수. 팝업·씬 왕복을 반복한 뒤 0 으로 돌아오는지 확인하는 용도다.
    /// 프로세스 메모리가 아니라 이 프로젝트가 소유권을 가진 객체 수만 센다.
    /// </summary>
    public static class ResourceCounters
    {
        public static int LiveAssets { get; private set; }
        public static int LiveInstances { get; private set; }
        public static int TotalAcquired { get; private set; }
        public static int StaleReleased { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            LiveAssets = 0;
            LiveInstances = 0;
            TotalAcquired = 0;
            StaleReleased = 0;
        }

        public static void AssetAcquired()
        {
            LiveAssets++;
            TotalAcquired++;
        }

        public static void AssetReleased()
        {
            LiveAssets--;
            if (LiveAssets < 0)
                LobbyLog.Error($"리소스 보유 수가 음수가 됐다: {LiveAssets}");
        }

        public static void InstanceCreated() => LiveInstances++;

        public static void InstanceDestroyed()
        {
            LiveInstances--;
            if (LiveInstances < 0)
                LobbyLog.Error($"인스턴스 수가 음수가 됐다: {LiveInstances}");
        }

        /// <summary>요청이 무효가 된 뒤 도착해 반영하지 않고 반환한 결과.</summary>
        public static void StaleResultReleased() => StaleReleased++;
    }
}
