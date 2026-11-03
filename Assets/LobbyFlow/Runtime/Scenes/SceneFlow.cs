using System;
using LobbyFlow.UI;
using UISystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LobbyFlow.Scenes
{
    /// <summary>
    /// Boot·로비·챕터 사이 씬 전환. 전환은 한 번에 하나만 진행하고, 진행 중 요청은 무시한다.
    /// 로딩 오버레이는 떠나는 씬이 소유하므로 그 씬이 언로드될 때 UIManager 가 함께 정리한다.
    /// 챕터 씬 이름은 <see cref="Chapters.ChapterDefinition"/> 이 갖는다.
    /// </summary>
    public static class SceneFlow
    {
        public const string Boot = "Boot";
        public const string Lobby = "Lobby";

        public static bool IsLoading { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => IsLoading = false;

        public static async void Load(string sceneName)
        {
            if (IsLoading)
            {
                LobbyLog.Info($"씬 전환 중이라 '{sceneName}' 요청을 무시한다");
                return;
            }

            IsLoading = true;
            LobbyLog.Info($"씬 전환 시작 → {sceneName}");

            try
            {
                LoadingOverlay overlay = null;
                var manager = UIManager.Instance;
                if (manager != null)
                    overlay = await manager.OpenAsync<LoadingOverlay>();

                if (overlay != null)
                    overlay.SetMessage($"{sceneName} 불러오는 중");

                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation == null)
                {
                    LobbyLog.Error($"'{sceneName}' 씬을 빌드 설정에서 찾지 못했다");
                    if (overlay != null && manager != null)
                        await manager.CloseAsync(overlay);
                    return;
                }

                while (!operation.isDone)
                {
                    // 씬이 바뀌면 오버레이는 이미 정리됐다.
                    if (overlay != null)
                        overlay.SetProgress(operation.progress);

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
    }
}
