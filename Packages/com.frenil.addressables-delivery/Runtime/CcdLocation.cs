using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
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
        private const string AppliedBadgeKey = "AddressablesDelivery.AppliedBadge";
        private const string BadgeSegment = "/release_by_badge/";

        private static string _badge;
        private static Func<IResourceLocation, string> _installedTransform;

        private static DeliverySettings Settings => DeliverySettings.Current;

        [Preserve] public static string ProjectId => Settings != null ? Settings.CcdProjectId : string.Empty;

        [Preserve] public static string Environment => Settings != null ? Settings.CcdEnvironment : string.Empty;

        [Preserve] public static string BucketId => Current.Id;

        /// <summary>
        /// 지금 받는 릴리스 배지. 출시 빌드는 <see cref="ReleaseBadge"/> 로 고정이다.
        /// 개발 빌드는 마지막으로 카탈로그 확인에 성공한 배지(기기에 저장)로 시작하고, 패치 서비스의 카탈로그 확인이
        /// 고른 배지(<see cref="TestBadge"/>)로 바꾼다. 받아 둔 카탈로그와 번들 주소가 늘 같은 릴리스를 가리키게 하려는 것이다.
        /// </summary>
        [Preserve] public static string Badge => _badge ??= Debug.isDebugBuild ? InitialTestBadge() : ReleaseBadge;

        /// <summary>다음 카탈로그 확인에서 쓸 배지. 개발 빌드에서 <see cref="Badge"/> 와 다르면 Boot 를 다시 실행할 때 바뀐다.</summary>
        public static string PendingBadge => Debug.isDebugBuild ? TestBadge : ReleaseBadge;

        public static string ReleaseBadge => Settings != null ? Settings.ReleaseBadge : "latest";

        public static string[] TestBadges => Settings != null ? Settings.TestBadges : new[] { ReleaseBadge };

        /// <summary>개발 빌드에서 고른 배지. 기기에 저장되고, 다음 카탈로그 확인(Boot)부터 적용된다.</summary>
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
            $"/buckets/{bucket.Id}{BadgeSegment}{badge}/entry_by_path/content/?path=/{fileName}";

        /// <summary>
        /// CCD 프로필에 넣을 원격 주소. 값은 실행 중에 <paramref name="className"/> 의 정적 속성
        /// ProjectId, Environment, BucketId, Badge 에서 읽는다. 파일 이름은 Addressables 가 뒤에 <c>/파일</c> 로 붙인다.
        /// </summary>
        public static string LoadUrlTemplate(string className) =>
            $"https://{{{className}.ProjectId}}.client-api.unity3dusercontent.com/client_api/v1/environments/{{{className}.Environment}}" +
            $"/buckets/{{{className}.BucketId}}{BadgeSegment}{{{className}.Badge}}/entry_by_path/content/?path=";

        /// <summary>
        /// 카탈로그 확인을 시작하며 고른 배지로 바꾼다. 돌려준 이전 배지는 확인이 실패하면 <see cref="RestoreBadge"/> 로 되돌린다.
        /// 이 시점에는 원격 번들을 쓰는 화면이 없어야 한다(Boot 에서만 부른다).
        /// </summary>
        public static string BeginBadgeCheck()
        {
            var previous = Badge;
            _badge = PendingBadge;
            if (previous != _badge)
                DeliveryLog.Info($"CCD 배지를 {previous} 에서 {_badge} 로 바꿔 카탈로그를 확인한다");
            return previous;
        }

        /// <summary>카탈로그 확인에 성공했다. 받아 둔 카탈로그가 이 배지의 것이므로 다음 시작도 이 배지로 한다.</summary>
        public static void CommitBadge()
        {
            if (!Debug.isDebugBuild || PlayerPrefs.GetString(AppliedBadgeKey, string.Empty) == _badge)
                return;

            PlayerPrefs.SetString(AppliedBadgeKey, _badge);
            PlayerPrefs.Save();
        }

        /// <summary>카탈로그 확인에 실패했다. 받아 둔 카탈로그는 이전 배지의 것이므로 번들도 이전 배지에서 받는다.</summary>
        public static void RestoreBadge(string previous)
        {
            if (previous != _badge)
                DeliveryLog.Info($"카탈로그 확인 실패로 CCD 배지를 {previous} 로 되돌린다");
            _badge = previous;
        }

        /// <summary>
        /// CCD 주소의 배지 부분을 <see cref="Badge"/> 로 바꾼다. 카탈로그의 주소는 Addressables 가 처음 읽은 배지 값으로 만들고
        /// 다시 읽지 않으므로(<c>AddressablesRuntimeProperties</c> 캐시), 요청할 때 바꿔야 실행 중에 배지를 옮길 수 있다.
        /// </summary>
        public static string ReplaceBadge(string url)
        {
            if (url == null || !url.StartsWith("https://", StringComparison.Ordinal))
                return url;

            var start = url.IndexOf(BadgeSegment, StringComparison.Ordinal);
            if (start < 0)
                return url;

            start += BadgeSegment.Length;
            var end = url.IndexOf('/', start);
            if (end < 0)
                return url;

            var badge = Badge;
            if (end - start == badge.Length && string.CompareOrdinal(url, start, badge, 0, badge.Length) == 0)
                return url;

            return url.Substring(0, start) + badge + url.Substring(end);
        }

        private static string InitialTestBadge()
        {
            // 처음 실행이면 아직 원격 카탈로그를 받지 않았으므로 고른 배지로 시작한다.
            var applied = PlayerPrefs.GetString(AppliedBadgeKey, string.Empty);
            return string.IsNullOrEmpty(applied) ? TestBadge : applied;
        }

        // 도메인 다시 읽기를 끈 에디터 플레이에서도 실행마다 배지를 다시 읽는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBadge() => _badge = null;

        // 출시 빌드는 배지가 바뀌지 않으므로 주소를 바꾸지 않는다. 프로젝트가 넣어 둔 변환이 있으면 그 결과에 이어서 적용한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBadgeTransform()
        {
            if (!Debug.isDebugBuild)
                return;

            var existing = Addressables.InternalIdTransformFunc;
            if (existing != null && existing == _installedTransform)
                return;

            _installedTransform = existing == null
                ? location => ReplaceBadge(location.InternalId)
                : location => ReplaceBadge(existing(location));
            Addressables.InternalIdTransformFunc = _installedTransform;
        }
    }
}
