using AddressablesDelivery.Patching;
using LobbyFlow.Content;
using LobbyFlow.Loading;
using UnityEngine;

namespace LobbyFlow
{
    /// <summary>
    /// Boot·로비·챕터가 함께 쓰는 서비스, 데이터, 선택 상태의 조립 지점.
    /// 로더와 패치 서비스 교체는 여기서만 한다.
    /// </summary>
    public static class LobbyServices
    {
        private static IPreviewAssetLoader _previewLoader;
        private static IPatchService _addressablesPatch;
        private static IPatchService _simulatedPatch;
        private static bool _contentLoading;

        public static IPreviewAssetLoader PreviewLoader => _previewLoader ??= CreatePreviewLoader();

        /// <summary>테스트 메뉴의 모의 패치 조건에 따라 실제·모의 서비스를 고른다. 호출할 때마다 현재 조건을 따른다.</summary>
        public static IPatchService PatchService => PatchSimulationSettings.IsSimulated
            ? _simulatedPatch ??= new SimulatedPatchService(ContentAddresses.PatchBootLabel)
            : _addressablesPatch ??= new AddressablesPatchService(ContentAddresses.PatchBootLabel);

        /// <summary>Boot 가 패치를 마친 뒤 읽은 데이터. 로비를 직접 실행했으면 <see cref="EnsureContentAsync"/> 가 채운다.</summary>
        public static ContentTables Content { get; private set; }

        /// <summary>던전으로 가져갈 선택 캐릭터. 비어 있으면 첫 캐릭터를 쓴다.</summary>
        public static string SelectedCharacterId { get; set; }

        /// <summary>선택 캐릭터가 입은 코스튬. 비어 있으면 기본 모습이다.</summary>
        public static string SelectedCostumeId { get; set; }

        /// <summary>입장할 챕터. 비어 있으면 첫 챕터를 쓴다.</summary>
        public static string SelectedChapterId { get; set; }

        /// <summary>다음 씬의 화면이 토스트로 보여 줄 안내. 떠나는 씬의 UI 는 씬과 함께 정리되므로 이렇게 넘긴다.</summary>
        public static string PendingNotice { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _previewLoader = null;
            _addressablesPatch = null;
            _simulatedPatch = null;
            _contentLoading = false;
            Content = null;
            SelectedCharacterId = null;
            SelectedCostumeId = null;
            SelectedChapterId = null;
            PendingNotice = null;
        }

        /// <summary>데이터를 다시 읽는다. Boot 가 패치 뒤에 부른다. 실패하면 <see cref="ContentDataException"/>.</summary>
        public static async Awaitable<ContentTables> ReloadContentAsync()
        {
            Content = null;
            Content = await ContentLoader.LoadAsync();
            return Content;
        }

        /// <summary>
        /// 데이터가 없으면 읽는다. 에디터에서 Boot 를 거치지 않고 로비나 챕터를 실행할 때를 위한 것이다.
        /// 여러 화면이 동시에 불러도 한 번만 읽는다.
        /// </summary>
        public static async Awaitable<ContentTables> EnsureContentAsync()
        {
            if (Content != null)
                return Content;

            // Awaitable 은 한 번만 기다릴 수 있으므로, 먼저 시작한 쪽만 읽고 나머지는 끝나기를 기다린다.
            if (_contentLoading)
            {
                while (_contentLoading)
                    await Awaitable.NextFrameAsync();

                return Content ?? throw new ContentDataException(null, 0, "데이터를 읽지 못했다");
            }

            _contentLoading = true;
            try
            {
                LobbyLog.Warning("Boot 를 거치지 않아 데이터를 지금 읽는다");
                return await ReloadContentAsync();
            }
            finally
            {
                _contentLoading = false;
            }
        }

        public static string ConsumeNotice()
        {
            var notice = PendingNotice;
            PendingNotice = null;
            return notice;
        }

        private static IPreviewAssetLoader CreatePreviewLoader()
        {
            IPreviewAssetLoader loader = new AddressablesPreviewAssetLoader();

            // 지연·실패 주입은 개발 빌드에서만 끼운다. 릴리스 빌드에는 테스트 메뉴도 없다.
            if (Debug.isDebugBuild)
                loader = new FaultInjectingPreviewLoader(loader);

            return loader;
        }
    }
}
