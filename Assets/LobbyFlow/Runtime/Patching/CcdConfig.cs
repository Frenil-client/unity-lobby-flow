using AddressablesDelivery;
using UnityEngine.Scripting;

namespace LobbyFlow.Patching
{
    /// <summary>
    /// CCD 프로필 주소가 실행 중에 읽는 값(SPEC 5.4). 값은 패키지의 <see cref="CcdLocation"/>(설정 에셋 <c>DeliverySettings</c>)에서 온다.
    /// 이미 CCD 에 올린 v1/v2 카탈로그의 번들 주소에 <c>{LobbyFlow.Patching.CcdConfig.BucketId}</c> 처럼 이 클래스 이름이 들어가 있어,
    /// 패키지 클래스로 바꾸면 카탈로그 내용이 달라져 업데이트 기준을 다시 만들어야 한다. 그래서 이름만 남겨 넘겨준다.
    /// 설정 에셋의 CCD 주소 클래스가 이 이름이다. 코드 제거(IL2CPP)로 빠지지 않게 <see cref="PreserveAttribute"/> 를 붙인다.
    /// </summary>
    [Preserve]
    public static class CcdConfig
    {
        [Preserve] public static string ProjectId => CcdLocation.ProjectId;

        [Preserve] public static string Environment => CcdLocation.Environment;

        [Preserve] public static string BucketId => CcdLocation.BucketId;

        [Preserve] public static string Badge => CcdLocation.Badge;
    }
}
