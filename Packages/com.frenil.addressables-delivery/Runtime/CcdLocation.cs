using UnityEngine;
using UnityEngine.Scripting;

namespace AddressablesDelivery
{
    /// <summary>
    /// CCD 다운로드 위치. production 같은 환경 하나, 플랫폼별 버킷, 배지(출시 빌드는 <see cref="ReleaseBadge"/>, 개발 빌드는 고른 배지)를 쓴다.
    /// CCD 프로필의 원격 주소가 <c>{AddressablesDelivery.CcdLocation.BucketId}</c> 처럼 실행 중에 리플렉션으로 읽으므로
    /// 코드 제거(IL2CPP)로 빠지지 않게 <see cref="PreserveAttribute"/> 를 붙인다.
    /// 이 클래스 이름은 카탈로그의 번들 주소에 그대로 들어가므로, 배포한 뒤에는 이름을 바꾸지 않는다.
    /// </summary>
    [Preserve]
    public static class CcdLocation
    {
        private const string TestBadgeKey = "AddressablesDelivery.TestBadge";

        private static string _badge;

        private static DeliverySettings Settings => DeliverySettings.Current;

        [Preserve] public static string ProjectId => Settings != null ? Settings.CcdProjectId : string.Empty;

        [Preserve] public static string Environment => Settings != null ? Settings.CcdEnvironment : string.Empty;

        [Preserve] public static string BucketId => Current.Id;

        /// <summary>
        /// 이번 실행에서 받는 릴리스 배지. 출시 빌드는 <see cref="ReleaseBadge"/>, 개발 빌드는 <see cref="TestBadge"/> 다.
        /// Addressables 가 시작할 때 주소를 한 번 읽어 두므로 처음 읽은 값으로 고정하고, 바꾼 배지는 앱을 다시 시작해야 적용된다.
        /// </summary>
        [Preserve] public static string Badge => _badge ??= Debug.isDebugBuild ? TestBadge : ReleaseBadge;

        public static string ReleaseBadge => Settings != null ? Settings.ReleaseBadge : "latest";

        public static string[] TestBadges => Settings != null ? Settings.TestBadges : new[] { ReleaseBadge };

        /// <summary>개발 빌드가 다음 시작부터 받을 배지. 기기에 저장된다.</summary>
        public static string TestBadge
        {
            get => PlayerPrefs.GetString(TestBadgeKey, TestBadges[0]);
            set
            {
                PlayerPrefs.SetString(TestBadgeKey, value);
                PlayerPrefs.Save();
            }
        }

        public static CcdBucket Windows => Settings != null ? Settings.WindowsBucket : default;
        public static CcdBucket Android => Settings != null ? Settings.AndroidBucket : default;
        public static CcdBucket IOS => Settings != null ? Settings.IosBucket : default;

        /// <summary>지금 빌드하거나 실행하는 플랫폼의 버킷. 에디터에서는 현재 빌드 대상을 따른다.</summary>
        public static CcdBucket Current
        {
            get
            {
#if UNITY_ANDROID
                return Android;
#elif UNITY_IOS
                return IOS;
#else
                return Windows;
#endif
            }
        }

        /// <summary>Addressables 빌드 대상 폴더 이름(<c>[BuildTarget]</c>)으로 버킷을 찾는다. 모르는 대상이면 false.</summary>
        public static bool TryGetBucket(string buildTarget, out CcdBucket bucket)
        {
            switch (buildTarget)
            {
                case "StandaloneWindows64":
                    bucket = Windows;
                    return true;
                case "Android":
                    bucket = Android;
                    return true;
                case "iOS":
                    bucket = IOS;
                    return true;
                default:
                    bucket = default;
                    return false;
            }
        }

        /// <summary>배지가 가리키는 릴리스의 파일 주소. CCD 프로필의 원격 주소와 같은 형식이다.</summary>
        public static string EntryUrl(CcdBucket bucket, string badge, string fileName) =>
            $"https://{ProjectId}.client-api.unity3dusercontent.com/client_api/v1/environments/{Environment}" +
            $"/buckets/{bucket.Id}/release_by_badge/{badge}/entry_by_path/content/?path=/{fileName}";

        /// <summary>
        /// CCD 프로필에 넣을 원격 주소. 값은 실행 중에 <paramref name="className"/> 의 정적 속성
        /// ProjectId, Environment, BucketId, Badge 에서 읽는다. 파일 이름은 Addressables 가 뒤에 <c>/파일</c> 로 붙인다.
        /// </summary>
        public static string LoadUrlTemplate(string className) =>
            $"https://{{{className}.ProjectId}}.client-api.unity3dusercontent.com/client_api/v1/environments/{{{className}.Environment}}" +
            $"/buckets/{{{className}.BucketId}}/release_by_badge/{{{className}.Badge}}/entry_by_path/content/?path=";

        // 도메인 다시 읽기를 끈 에디터 플레이에서도 실행마다 배지를 다시 읽는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBadge() => _badge = null;
    }
}
