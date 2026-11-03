using UnityEngine;

namespace LobbyFlow
{
    public static class AppQuit
    {
        /// <summary>에디터에서는 플레이를 멈추고, 플레이어에서는 앱을 끝낸다.</summary>
        public static void Quit()
        {
            LobbyLog.Info("앱 종료 요청");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
