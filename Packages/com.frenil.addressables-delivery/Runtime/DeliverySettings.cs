using System;
using UnityEngine;

namespace AddressablesDelivery
{
    /// <summary>원격 콘텐츠를 올리고 받는 곳.</summary>
    public enum RemoteStorage
    {
        /// <summary>Unity Cloud Content Delivery. 플랫폼별 버킷과 배지로 받는다.</summary>
        Ccd,

        /// <summary>다른 저장소(S3, R2, 자체 서버 등). 받는 주소와 올리는 명령을 직접 적는다.</summary>
        Custom,
    }

    /// <summary>CCD 버킷 하나. 이름은 업로드(UGS CLI)가, ID 는 다운로드 주소가 쓴다.</summary>
    [Serializable]
    public struct CcdBucket
    {
        [SerializeField] private string _name;
        [SerializeField] private string _id;

        public CcdBucket(string name, string id)
        {
            _name = name;
            _id = id;
        }

        public string Name => _name ?? string.Empty;
        public string Id => _id ?? string.Empty;
    }

    /// <summary>
    /// 프로젝트마다 다른 값을 모은 설정 에셋. 도구 코드는 이 값만 읽는다.
    /// 플레이어가 실행 중에 CCD 주소를 읽으므로 <c>Resources</c> 폴더에 <see cref="ResourceName"/> 이름으로 둔다
    /// (Tools ▸ Addressables Delivery ▸ 프로젝트에 적용 이 없으면 <c>Assets/Resources</c> 에 만든다).
    /// 프로젝트 ID 와 버킷 ID 는 공개 다운로드 주소의 일부라 비밀값이 아니다. 업로드 키는 여기에 두지 않는다.
    /// </summary>
    public sealed class DeliverySettings : ScriptableObject
    {
        public const string ResourceName = "DeliverySettings";

        [Header("원격 저장소")]
        [SerializeField] private RemoteStorage _storage = RemoteStorage.Ccd;

        [Header("CCD")]
        [SerializeField] private string _ccdProjectId = string.Empty;
        [SerializeField] private string _ccdEnvironment = "production";
        [SerializeField] private CcdBucket _windowsBucket;
        [SerializeField] private CcdBucket _androidBucket;
        [SerializeField] private CcdBucket _iosBucket;

        [Tooltip("출시 빌드가 받는 배지. latest 는 CCD 가 새 릴리스마다 옮긴다.")]
        [SerializeField] private string _releaseBadge = "latest";

        [Tooltip("개발 빌드에서 고를 수 있는 배지. 첫 값이 기본이다.")]
        [SerializeField] private string[] _testBadges = { "latest" };

        [Tooltip("CCD 프로필 주소가 실행 중 값을 읽는 클래스. 이미 배포한 카탈로그가 다른 클래스를 가리키면 그 이름을 둔다.")]
        [SerializeField] private string _ccdUrlClass = "AddressablesDelivery.CcdLocation";

        [Header("다른 저장소 (Custom)")]
        [Tooltip("원격 그룹의 받는 주소. [BuildTarget] 같은 빌드 시점 변수를 쓸 수 있다.")]
        [SerializeField] private string _customLoadUrl = "https://cdn.example.com/[BuildTarget]";

        [Tooltip("원격 빌드 폴더를 올리는 명령. {folder} {platform} {badge} {notes} 를 바꿔 넣고, Windows 는 cmd /c, 그 외는 /bin/sh -c 로 실행한다.")]
        [SerializeField] private string _customUploadCommand = string.Empty;

        [Header("빌드")]
        [Tooltip("공통 번들(내장 셰이더, 스크립트) 이름 접두어. 비우면 프로젝트 폴더 이름 해시가 들어가 빌드하는 곳마다 번들 해시가 달라진다.")]
        [SerializeField] private string _sharedBundlePrefix = string.Empty;

        [Tooltip("플레이어 빌드 파일 이름(확장자 제외).")]
        [SerializeField] private string _playerName = string.Empty;

        [Tooltip("빌드 전과 저장 직전에 동적 데이터를 비울 TMP 폰트 에셋 폴더. 비우면 하지 않는다.")]
        [SerializeField] private string[] _dynamicFontFolders = Array.Empty<string>();

        [Tooltip("LocalServer 프로필과 에디터 콘텐츠 서버 포트.")]
        [SerializeField] private int _localServerPort = 8080;

        private static DeliverySettings _current;

        public RemoteStorage Storage => _storage;
        public string CcdProjectId => _ccdProjectId ?? string.Empty;
        public string CcdEnvironment => _ccdEnvironment ?? string.Empty;
        public CcdBucket WindowsBucket => _windowsBucket;
        public CcdBucket AndroidBucket => _androidBucket;
        public CcdBucket IosBucket => _iosBucket;
        public string ReleaseBadge => string.IsNullOrEmpty(_releaseBadge) ? "latest" : _releaseBadge;
        public string[] TestBadges => _testBadges != null && _testBadges.Length > 0 ? _testBadges : new[] { ReleaseBadge };
        public string CcdUrlClass => string.IsNullOrEmpty(_ccdUrlClass) ? typeof(CcdLocation).FullName : _ccdUrlClass;
        public string CustomLoadUrl => _customLoadUrl ?? string.Empty;
        public string CustomUploadCommand => _customUploadCommand ?? string.Empty;
        public string SharedBundlePrefix => _sharedBundlePrefix ?? string.Empty;
        public string PlayerName => _playerName ?? string.Empty;
        public string[] DynamicFontFolders => _dynamicFontFolders ?? Array.Empty<string>();
        public int LocalServerPort => _localServerPort > 0 ? _localServerPort : 8080;

        /// <summary>
        /// 프로젝트의 설정 에셋. 플레이어는 Resources 에서, 에디터는 에셋 데이터베이스에서 찾는다. 없으면 null.
        /// </summary>
        public static DeliverySettings Current
        {
            get
            {
                if (_current == null)
                    _current = Find();

                return _current;
            }
        }

        /// <summary>에디터 도구가 새로 만든 에셋을 바로 쓰게 한다.</summary>
        public static void SetCurrent(DeliverySettings settings) => _current = settings;

        // CCD 주소는 Addressables 초기화 때 읽힌다. 그 전에 메인 스레드에서 한 번 찾아 둔다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Preload() => _ = Current;

        private static DeliverySettings Find()
        {
#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(DeliverySettings));
            if (guids.Length > 1)
                Debug.LogWarning($"[Delivery] {nameof(DeliverySettings)} 에셋이 {guids.Length}개다. 하나만 둔다. 첫 번째를 쓴다.");

            return guids.Length > 0
                ? UnityEditor.AssetDatabase.LoadAssetAtPath<DeliverySettings>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
#else
            var settings = Resources.Load<DeliverySettings>(ResourceName);
            if (settings == null)
                Debug.LogError($"[Delivery] Resources/{ResourceName} 가 없다. 원격 주소를 만들 수 없다.");

            return settings;
#endif
        }
    }
}
