using LobbyFlow.Loading;
using UnityEngine;

namespace LobbyFlow
{
    /// <summary>로비와 던전이 함께 쓰는 서비스의 조립 지점. 로더 교체(Addressables 도입)는 여기서만 한다.</summary>
    public static class LobbyServices
    {
        private static IPreviewAssetLoader _previewLoader;

        public static IPreviewAssetLoader PreviewLoader => _previewLoader ??= CreatePreviewLoader();

        /// <summary>던전으로 가져갈 선택 캐릭터. 비어 있으면 목록의 첫 캐릭터를 쓴다.</summary>
        public static string SelectedCharacterId { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _previewLoader = null;
            SelectedCharacterId = null;
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
