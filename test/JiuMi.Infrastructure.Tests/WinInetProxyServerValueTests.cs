using JiuMi.Infrastructure.WeChatVideo;

namespace JiuMi.Infrastructure.Tests;

public sealed class WinInetProxyServerValueTests
{
    [Theory]
    [InlineData(null, "https=127.0.0.1:1234")]
    [InlineData("", "https=127.0.0.1:1234")]
    [InlineData("   ", "https=127.0.0.1:1234")]
    [InlineData("127.0.0.1:7897", "http=127.0.0.1:7897;https=127.0.0.1:1234")]
    [InlineData(
        "http=127.0.0.1:7897;https=127.0.0.1:9999",
        "http=127.0.0.1:7897;https=127.0.0.1:1234")]
    [InlineData(
        "ftp=127.0.0.1:21;http=127.0.0.1:7897",
        "ftp=127.0.0.1:21;http=127.0.0.1:7897;https=127.0.0.1:1234")]
    public void ForHttps_GivenExistingValue_ReplacesOnlyHttps(string? existing, string expected)
    {
        Assert.Equal(expected, WinInetProxyServerValue.ForHttps(existing, 1234));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void ForHttps_GivenInvalidPort_Throws(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WinInetProxyServerValue.ForHttps("127.0.0.1:7897", port));
    }
}
