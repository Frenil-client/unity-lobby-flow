using System;

namespace LobbyFlow.Loading
{
    public sealed class PreviewLoadException : Exception
    {
        public PreviewLoadException(string key, string reason, bool injected = false)
            : base($"'{key}' 미리보기 로드 실패: {reason}")
        {
            Key = key;
            IsInjected = injected;
        }

        public string Key { get; }

        /// <summary>테스트 메뉴가 주입한 실패인지. 결과 기록에서 실제 실패와 구분한다.</summary>
        public bool IsInjected { get; }
    }
}
