using UnityEngine;
using UnityEngine.Scripting;

namespace LobbyFlow.Patching
{
    /// <summary>CCD 버킷 하나. 이름은 업로드(CLI)가, ID 는 다운로드 주소가 쓴다.</summary>
    public readonly struct CcdBucket
    {
        public readonly string Name;
        public readonly string Id;

        public CcdBucket(string name, string id)
        {
            Name = name;
            Id = id;
        }
    }

    /// <summary>
    /// CCD 위치(SPEC 5.4, 5.5). production 환경, 플랫폼별 버킷, 배지(출시 빌드 <c>latest</c>, 개발 빌드는 고른 버전)를 쓴다.
    /// 프로젝트 ID 와 버킷 ID 는 공개 다운로드 주소의 일부라 비밀값이 아니다. 업로드용 서비스 계정 키는 저장소에 두지 않는다.
    /// CCD 프로필의 원격 주소가 <c>{LobbyFlow.Patching.CcdConfig.BucketId}</c> 처럼 실행 중에 리플렉션으로 읽으므로,
    /// 코드 제거(IL2CPP)로 빠지지 않게 <see cref="PreserveAttribute"/> 를 붙인다.
    /// </summary>
    [Preserve]
    public static class CcdConfig
    {
        // Unity Cloud 프로젝트(조직 frenil, unity-lobby-flow)와 production 환경의 공개 버킷.
        public const string ProjectId = "9211a594-db04-43bd-bd55-c6d120db13de";
        public const string Environment = "production";

        public static readonly CcdBucket Windows = new("lobby-flow-windows", "524e51c0-7d7e-459b-82e0-ba2724966f88");
        public static readonly CcdBucket Android = new("lobby-flow-android", "1454aff5-6030-4ea2-bf7d-ea924c51b8f8");
        public static readonly CcdBucket IOS = new("lobby-flow-ios", "cd31d4d9-4d68-4692-bcc8-aa1e3bb32c9e");

        /// <summary>출시 빌드가 받는 배지. CCD 가 새 릴리스마다 옮긴다.</summary>
        public const string LatestBadge = "latest";

        /// <summary>개발 빌드의 테스트 메뉴에서 고를 수 있는 배지. v1, v2 는 콘텐츠 버전 릴리스에 붙인다(SPEC 5.4).</summary>
        public static readonly string[] TestBadges = { "v1", "v2", LatestBadge };

        private const string TestBadgeKey = "LobbyFlow.CcdTestBadge";

        private static string _badge;

        [Preserve] public static string BucketId => Current.Id;

        /// <summary>
        /// 이번 실행에서 받는 릴리스 배지. 출시 빌드는 항상 latest, 개발 빌드는 테스트 메뉴에서 고른 배지(처음에는 v1)다.
        /// Addressables 가 시작할 때 주소를 한 번 읽어 두므로 처음 읽은 값으로 고정하고, 바꾼 배지는 앱을 다시 시작해야 적용된다.
        /// </summary>
        [Preserve] public static string Badge => _badge ??= Debug.isDebugBuild ? TestBadge : LatestBadge;

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

        /// <summary>배지가 가리키는 릴리스의 파일 주소. CCD 프로필의 원격 주소와 같은 형식이다.</summary>
        public static string EntryUrl(CcdBucket bucket, string badge, string fileName) =>
            $"https://{ProjectId}.client-api.unity3dusercontent.com/client_api/v1/environments/{Environment}" +
            $"/buckets/{bucket.Id}/release_by_badge/{badge}/entry_by_path/content/?path=/{fileName}";

        // 도메인 다시 읽기를 끈 에디터 플레이에서도 실행마다 배지를 다시 읽는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBadge() => _badge = null;

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
    }
}
