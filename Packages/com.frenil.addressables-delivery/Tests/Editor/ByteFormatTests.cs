using AddressablesDelivery.Patching;
using NUnit.Framework;

namespace AddressablesDelivery.Tests
{
    /// <summary>다운로드 크기 표시. 받을 것이 있는데 0.0MB 처럼 보이지 않는지, 단위 경계가 맞는지 본다.</summary>
    public sealed class ByteFormatTests
    {
        [TestCase(0L, "0KB")]
        [TestCase(1L, "1KB")]
        [TestCase(300L, "1KB")]
        [TestCase(1024L, "1KB")]
        [TestCase(1025L, "2KB")]
        [TestCase(70L * 1024, "70KB")]
        [TestCase(1023L * 1024, "1023KB")]
        [TestCase(1023L * 1024 + 1, "1.0MB")]
        [TestCase(1024L * 1024, "1.0MB")]
        [TestCase(3040870L, "2.9MB")]
        [TestCase(1024L * 1024 * 1023, "1023.0MB")]
        [TestCase(1024L * 1024 * 1024 - 1, "1.00GB")]
        [TestCase(1024L * 1024 * 1024, "1.00GB")]
        [TestCase(-5L, "0KB")]
        public void FormatPicksUnitBySize(long bytes, string expected)
        {
            Assert.That(ByteFormat.Format(bytes), Is.EqualTo(expected));
        }

        [TestCase(0L, 70L * 1024, "0KB / 70KB")]
        [TestCase(400L * 1024, 3040870L, "0.4MB / 2.9MB")]
        [TestCase(3040870L, 3040870L, "2.9MB / 2.9MB")]
        public void ProgressUsesTotalUnit(long downloaded, long total, string expected)
        {
            Assert.That(ByteFormat.FormatProgress(downloaded, total), Is.EqualTo(expected));
        }
    }
}
