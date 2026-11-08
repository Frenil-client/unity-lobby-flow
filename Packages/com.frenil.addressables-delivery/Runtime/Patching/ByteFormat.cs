using System;
using System.Globalization;

namespace AddressablesDelivery.Patching
{
    /// <summary>
    /// 다운로드 크기 표시. 1MB 미만은 KB(올림 정수), 1GB 미만은 MB(소수 한 자리), 그 이상은 GB(소수 두 자리)로 쓴다.
    /// 받을 것이 있는데 0으로 보이지 않게 KB 는 올림한다(300바이트 → 1KB).
    /// </summary>
    public static class ByteFormat
    {
        private const long Kilobyte = 1024L;
        private const long Megabyte = Kilobyte * 1024L;
        private const long Gigabyte = Megabyte * 1024L;

        private enum Unit
        {
            KB,
            MB,
            GB,
        }

        /// <summary>크기에 맞는 단위로 쓴다. 예: 0KB, 70KB, 2.9MB, 1.25GB.</summary>
        public static string Format(long bytes) => Format(bytes, UnitOf(bytes));

        /// <summary>
        /// 진행률처럼 두 값을 함께 보일 때 전체 크기의 단위로 맞춘다. 예: 0.4MB / 2.9MB, 35KB / 70KB.
        /// 받은 크기만 단위가 바뀌어 숫자가 줄어드는 것처럼 보이지 않게 한다.
        /// </summary>
        public static string FormatProgress(long downloadedBytes, long totalBytes)
        {
            var unit = UnitOf(totalBytes);
            return $"{Format(downloadedBytes, unit)} / {Format(totalBytes, unit)}";
        }

        // 올림이나 반올림 뒤 1024 가 되는 값(1024KB, 1024.0MB)은 윗 단위로 쓴다.
        private static Unit UnitOf(long bytes) =>
            bytes >= Gigabyte - Megabyte / 20 ? Unit.GB : bytes > Megabyte - Kilobyte ? Unit.MB : Unit.KB;

        private static string Format(long bytes, Unit unit)
        {
            bytes = Math.Max(0L, bytes);
            return unit switch
            {
                Unit.GB => ((double)bytes / Gigabyte).ToString("0.00", CultureInfo.InvariantCulture) + "GB",
                Unit.MB => ((double)bytes / Megabyte).ToString("0.0", CultureInfo.InvariantCulture) + "MB",
                _ => $"{(bytes + Kilobyte - 1) / Kilobyte}KB",
            };
        }
    }
}
