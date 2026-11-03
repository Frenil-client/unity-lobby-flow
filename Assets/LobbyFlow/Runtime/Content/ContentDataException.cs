using System;

namespace LobbyFlow.Content
{
    /// <summary>데이터 파일의 형식이나 참조가 잘못됐다. Boot 에서 오류로 처리하고 로비로 가지 않는다(SPEC 5.6).</summary>
    public sealed class ContentDataException : Exception
    {
        public ContentDataException(string source, int line, string reason)
            : base($"{source ?? "데이터"}:{line} {reason}")
        {
            FileName = source;
            Line = line;
        }

        public string FileName { get; }
        public int Line { get; }
    }
}
