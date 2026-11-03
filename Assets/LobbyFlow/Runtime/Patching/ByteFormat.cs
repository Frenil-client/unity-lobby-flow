namespace LobbyFlow.Patching
{
    public static class ByteFormat
    {
        private const double Megabyte = 1024d * 1024d;

        public static string ToMegabytes(long bytes) => $"{bytes / Megabyte:0.0}MB";
    }
}
