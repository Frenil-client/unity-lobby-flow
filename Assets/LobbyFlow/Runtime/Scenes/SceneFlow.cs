using System;
using LobbyFlow.Content;
using LobbyFlow.UI;
using UISystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace LobbyFlow.Scenes
{
    /// <summary>
    /// Boot·로비(빌드 씬)와 챕터(Addressables 씬) 사이 전환. 전환은 한 번에 하나만 진행하고, 진행 중 요청은 무시한다.
    /// 로딩 오버레이는 떠나는 씬이 소유하므로 그 씬이 언로드될 때 UIManager 가 함께 정리한다.
    /// 챕터 씬 핸들은 씬이 언로드될 때 Addressables 가 반환한다(ReleaseSceneWhenSceneUnloaded). 따로 들고 있지 않는다.
    /// </summary>
    public static class SceneFlow
    {
        public const string Boot = "Boot";
        public const string Lobby = "Lobby";

        /// <summary>AsyncOperation.progress 는 활성화 직전 이 값에서 멈춘다.</summary>
        private const float SceneActivationProgress = 0.9f;

        public static bool IsLoading { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => IsLoading = false;

        /// <summary>빌드 설정에 등록된 씬으로 간다.</summary>
        public static async void Load(string sceneName)
        {
            if (!TryBegin(sceneName))
                return;

            try
            {
                var overlay = await OpenOverlayAsync($"{sceneName} 불러오는 중");
                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation == null)
                {
                    LobbyLog.Error($"'{sceneName}' 씬을 빌드 설정에서 찾지 못했다");
                    await CloseOverlayAsync(overlay);
                    return;
                }

                while (!operation.isDone)
                {
                    // 씬이 바뀌면 오버레이는 이미 정리됐다.
                    if (overlay != null)
                        overlay.SetProgress(operation.progress / SceneActivationProgress);

                    await Awaitable.NextFrameAsync();
                }

                LobbyLog.Info($"씬 전환 완료 → {sceneName}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>챕터에 들어간다. 원격 챕터는 받을 것이 있으면 다운로드 팝업을 먼저 거친다(SPEC 6.1).</summary>
        public static async void EnterChapter(ChapterRecord chapter)
        {
            if (chapter == null || !TryBegin(chapter.Name))
                return;

            try
            {
                if (chapter.RequiresDownload && !await ChapterDownload.EnsureAsync(chapter))
                {
                    LobbyLog.Info($"챕터 진입 취소 {chapter.Id}");
                    return;
                }

                var overlay = await OpenOverlayAsync($"{chapter.Name} 불러오는 중");
                var handle = Addressables.LoadSceneAsync(chapter.SceneAddress, LoadSceneMode.Single);
                while (!handle.IsDone)
                {
                    if (overlay != null)
                        overlay.SetProgress(handle.PercentComplete);

                    await Awaitable.NextFrameAsync();
                }

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    // 씬이 올라오지 않았으므로 자동 반환 대상이 아니다. 여기서 반환한다.
                    LobbyLog.Error($"챕터 씬 로드 실패 '{chapter.SceneAddress}': {handle.OperationException?.Message}");
                    Addressables.Release(handle);
                    await CloseOverlayAsync(overlay);
                    MessageToast.Show("챕터를 불러오지 못했습니다.");
                    return;
                }

                LobbyLog.Info($"씬 전환 완료 → {chapter.SceneAddress}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static bool TryBegin(string target)
        {
            if (IsLoading)
            {
                LobbyLog.Info($"씬 전환 중이라 '{target}' 요청을 무시한다");
                return false;
            }

            IsLoading = true;
            LobbyLog.Info($"씬 전환 시작 → {target}");
            return true;
        }

        private static async Awaitable<LoadingOverlay> OpenOverlayAsync(string message)
        {
            var manager = UIManager.Instance;
            var overlay = manager != null ? await manager.OpenAsync<LoadingOverlay>() : null;
            if (overlay != null)
                overlay.SetMessage(message);

            return overlay;
        }

        private static async Awaitable CloseOverlayAsync(LoadingOverlay overlay)
        {
            var manager = UIManager.Instance;
            if (overlay != null && manager != null)
                await manager.CloseAsync(overlay);
        }
    }
}
