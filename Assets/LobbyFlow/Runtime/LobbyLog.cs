using UnityEngine;

namespace LobbyFlow
{
    /// <summary>
    /// 로딩 흐름 검증용 로그. 반복 실행 결과를 로그로 대조하므로 접두어와 프레임 번호를 고정 형식으로 남긴다.
    /// </summary>
    public static class LobbyLog
    {
        private const string Prefix = "[LobbyFlow]";

        public static void Info(string message) => Debug.Log($"{Prefix} f{Time.frameCount} {message}");

        public static void Warning(string message) => Debug.LogWarning($"{Prefix} f{Time.frameCount} {message}");

        public static void Error(string message) => Debug.LogError($"{Prefix} f{Time.frameCount} {message}");
    }
}
