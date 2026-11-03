using LobbyFlow.Loading;
using LobbyFlow.Patching;
using UnityEngine;

namespace LobbyFlow
{
    /// <summary>
    /// 로비·던전·Boot 가 함께 쓰는 서비스와 선택 상태의 조립 지점.
    /// 로더와 패치 서비스 교체(Addressables 도입)는 여기서만 한다.
    /// </summary>
    public static class LobbyServices
    {
        private static IPreviewAssetLoader _previewLoader;
        private static IPatchService _patchService;

        public static IPreviewAssetLoader PreviewLoader => _previewLoader ??= CreatePreviewLoader();
        public static IPatchService PatchService => _patchService ??= new SimulatedPatchService();

        /// <summary>던전으로 가져갈 선택 캐릭터. 비어 있으면 목록의 첫 캐릭터를 쓴다.</summary>
        public static string SelectedCharacterId { get; set; }

        /// <summary>선택 캐릭터가 입은 코스튬. 비어 있으면 기본 모습이다.</summary>
        public static string SelectedCostumeId { get; set; }

        /// <summary>입장할 챕터. 비어 있으면 목록의 첫 챕터를 쓴다.</summary>
        public static string SelectedChapterId { get; set; }

        /// <summary>다음 씬의 화면이 토스트로 보여 줄 안내. 떠나는 씬의 UI 는 씬과 함께 정리되므로 이렇게 넘긴다.</summary>
        public static string PendingNotice { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _previewLoader = null;
            _patchService = null;
            SelectedCharacterId = null;
            SelectedCostumeId = null;
            SelectedChapterId = null;
            PendingNotice = null;
        }

        public static string ConsumeNotice()
        {
            var notice = PendingNotice;
            PendingNotice = null;
            return notice;
        }

        private static IPreviewAssetLoader CreatePreviewLoader()
        {
            IPreviewAssetLoader loader = new DirectPreviewAssetLoader();

            // 지연·실패 주입은 개발 빌드에서만 끼운다. 릴리스 빌드에는 테스트 메뉴도 없다.
            if (Debug.isDebugBuild)
                loader = new FaultInjectingPreviewLoader(loader);

            return loader;
        }
    }
}
