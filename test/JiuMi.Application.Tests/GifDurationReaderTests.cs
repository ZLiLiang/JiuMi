using JiuMi.Application.Startup;

namespace JiuMi.Application.Tests;

public sealed class GifDurationReaderTests
{
    [Fact]
    public void TryRead_GivenOneFrameOfTwentyCentiseconds_ReadsTwoHundredMilliseconds()
    {
        using Stream gif = CreateGif([20]);

        Assert.True(GifDurationReader.TryRead(gif, out TimeSpan duration));

        // Decoder rule: delayMs = delayCentiseconds * 10.
        Assert.Equal(TimeSpan.FromMilliseconds(200), duration);
    }

    [Fact]
    public void TryRead_GivenOneFrameOfOneCentisecond_ReadsOneHundredMilliseconds()
    {
        using Stream gif = CreateGif([1]);

        Assert.True(GifDurationReader.TryRead(gif, out TimeSpan duration));

        // One centisecond is 10ms, which is at the threshold, so the decoder counts a hundred instead.
        Assert.Equal(TimeSpan.FromMilliseconds(100), duration);
    }

    [Fact]
    public void TryRead_GivenFramesOfTwentyAndThirtyCentiseconds_ReadsFiveHundredMilliseconds()
    {
        using Stream gif = CreateGif([20, 30]);

        Assert.True(GifDurationReader.TryRead(gif, out TimeSpan duration));

        Assert.Equal(TimeSpan.FromMilliseconds(500), duration);
    }

    [Fact]
    public void TryRead_GivenBytesThatAreNotAGif_ReturnsFalse()
    {
        using Stream notAGif = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Assert.False(GifDurationReader.TryRead(notAGif, out TimeSpan duration));

        Assert.Equal(TimeSpan.Zero, duration);
    }

    [Fact]
    public void TryRead_GivenAStreamCutShortBeforeTheTrailer_ReturnsFalse()
    {
        byte[] complete = EncodeGif([20]);
        using Stream truncated = new MemoryStream(complete, 0, complete.Length - 1);

        Assert.False(GifDurationReader.TryRead(truncated, out TimeSpan duration));

        Assert.Equal(TimeSpan.Zero, duration);
    }

    /// <summary>
    /// Builds a GIF whose frames carry the given graphic-control delays: a 1x1 pixel image per frame
    /// and no colour table.
    /// </summary>
    /// <param name="delayCentiseconds">The per-frame delays to embed.</param>
    /// <returns>A stream over the encoded bytes.</returns>
    private static MemoryStream CreateGif(IReadOnlyList<int> delayCentiseconds) =>
        new(EncodeGif(delayCentiseconds));

    private static byte[] EncodeGif(IReadOnlyList<int> delayCentiseconds)
    {
        var bytes = new List<byte>();
        bytes.AddRange("GIF89a"u8.ToArray());
        bytes.AddRange([0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00]);

        foreach (int delay in delayCentiseconds)
        {
            bytes.AddRange([0x21, 0xF9, 0x04, 0x00, (byte)(delay & 0xFF), (byte)((delay >> 8) & 0xFF), 0x00, 0x00]);
            bytes.AddRange([0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]);
            bytes.AddRange([0x02, 0x01, 0xFF, 0x00]);
        }

        bytes.Add(0x3B);
        return [.. bytes];
    }
}
