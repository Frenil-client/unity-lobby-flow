using UnityEngine;

namespace AddressablesDelivery.Loading
{
    /// <summary>
    /// 소유자가 기록한 Addressables 리소스와 생성 인스턴스 수. 화면 열고 닫기, 씬 왕복을 반복한 뒤 시작 값으로 돌아오는지 확인하는 용도다.
    /// 프로세스 메모리가 아니라 소유권을 가진 객체 수만 센다. <see cref="AssetHandleSet"/> 은 스스로 기록하고, 직접 로드하는 코드는 Acquired/Released 를 부른다.
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
                DeliveryLog.Error($"리소스 보유 수가 음수가 됐다: {LiveAssets}");
        }

        public static void InstanceCreated() => LiveInstances++;

        public static void InstanceDestroyed()
        {
            LiveInstances--;
            if (LiveInstances < 0)
                DeliveryLog.Error($"인스턴스 수가 음수가 됐다: {LiveInstances}");
        }

        /// <summary>요청이 무효가 된 뒤 도착해 반영하지 않고 반환한 결과.</summary>
        public static void StaleResultReleased() => StaleReleased++;
    }
}
