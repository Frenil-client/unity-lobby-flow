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
    /// CCD 위치(SPEC 5.4, 5.5). production 환경, 플랫폼별 버킷, <c>latest</c> 배지를 쓴다.
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
        public const string Badge = "latest";

        public static readonly CcdBucket Windows = new("lobby-flow-windows", "524e51c0-7d7e-459b-82e0-ba2724966f88");
        public static readonly CcdBucket Android = new("lobby-flow-android", "1454aff5-6030-4ea2-bf7d-ea924c51b8f8");
        public static readonly CcdBucket IOS = new("lobby-flow-ios", "cd31d4d9-4d68-4692-bcc8-aa1e3bb32c9e");

        [Preserve] public static string BucketId => Current.Id;

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
