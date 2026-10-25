namespace JiuMi.Application.Startup;

/// <summary>
/// Measures one pass of a GIF by summing its frame delays, using the rounding rule the decoder
/// applies so that the application's own timer matches what is drawn.
/// </summary>
public static class GifDurationReader
{
    private const int SignatureLength = 6;
    private const int ScreenDescriptorLength = 7;
    private const int ImageDescriptorLength = 9;
    private const int ReadChunkLength = 256;
    private const byte ExtensionIntroducer = 0x21;
    private const byte GraphicControlLabel = 0xF9;
    private const byte ImageIntroducer = 0x2C;
    private const byte BlockTerminator = 0x00;
    private const byte Trailer = 0x3B;

    /// <summary>
    /// Reads the length of a single playback pass from the frame delays of a GIF.
    /// </summary>
    /// <param name="stream">The GIF bytes, read from the current position to the end.</param>
    /// <param name="duration">The summed frame delays, or zero when the stream is not a usable GIF.</param>
    /// <returns>True when the structure parsed and at least one frame was found.</returns>
    public static bool TryRead(Stream stream, out TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(stream);

        duration = TimeSpan.Zero;
        if (!TrySkipHeader(stream))
        {
            return false;
        }

        long totalMilliseconds = 0;
        int frameCount = 0;
        int pendingDelayCentiseconds = 0;

        while (true)
        {
            switch (stream.ReadByte())
            {
                case Trailer:
                    if (frameCount == 0)
                    {
                        return false;
                    }

                    duration = TimeSpan.FromMilliseconds(totalMilliseconds);
                    return true;
                case ExtensionIntroducer:
                    if (stream.ReadByte() == GraphicControlLabel)
                    {
                        if (!TryReadDelayCentiseconds(stream, out pendingDelayCentiseconds))
                        {
                            return false;
                        }
                    }
                    else if (!SkipDataBlocks(stream))
                    {
                        return false;
                    }

                    break;
                case ImageIntroducer:
                    if (!TrySkipImage(stream))
                    {
                        return false;
                    }

                    totalMilliseconds += FrameDelayMilliseconds(pendingDelayCentiseconds);
                    frameCount++;

                    // A frame with no graphic control of its own does not inherit the previous delay.
                    pendingDelayCentiseconds = 0;
                    break;
                default:
                    // An unrecognised introducer means the offsets no longer line up with the blocks.
                    return false;
            }
        }
    }

    /// <summary>
    /// Gets the delay the decoder gives a frame, in milliseconds.
    /// </summary>
    /// <param name="delayCentiseconds">The delay stored in the frame's graphic control extension.</param>
    /// <returns>The milliseconds the frame is on screen for.</returns>
    public static int FrameDelayMilliseconds(int delayCentiseconds)
    {
        int milliseconds = delayCentiseconds * 10;

        // A frame shorter than 10ms is invisible, so the decoder substitutes a hundred; the splash
        // timer has to count the same frames or it stops too early.
        return milliseconds <= 10 ? 100 : milliseconds;
    }

    private static bool TrySkipHeader(Stream stream)
    {
        Span<byte> signature = stackalloc byte[SignatureLength];
        if (!TryReadExactly(stream, signature) || !IsGifSignature(signature))
        {
            return false;
        }

        Span<byte> screenDescriptor = stackalloc byte[ScreenDescriptorLength];
        if (!TryReadExactly(stream, screenDescriptor))
        {
            return false;
        }

        // The global colour table follows the descriptor whenever the packed flags announce it.
        return Skip(stream, ColorTableByteLength(screenDescriptor[4]));
    }

    private static bool IsGifSignature(ReadOnlySpan<byte> signature) =>
        signature[0] == (byte)'G' &&
        signature[1] == (byte)'I' &&
        signature[2] == (byte)'F' &&
        signature[3] == (byte)'8' &&
        (signature[4] == (byte)'7' || signature[4] == (byte)'9') &&
        signature[5] == (byte)'a';

    private static bool TryReadDelayCentiseconds(Stream stream, out int delayCentiseconds)
    {
        delayCentiseconds = 0;
        int blockSize = stream.ReadByte();
        if (blockSize < 3)
        {
            return false;
        }

        Span<byte> block = stackalloc byte[blockSize];
        if (!TryReadExactly(stream, block))
        {
            return false;
        }

        // The little-endian delay pair sits after the packed flags.
        delayCentiseconds = block[1] | (block[2] << 8);
        return SkipDataBlocks(stream);
    }

    private static bool TrySkipImage(Stream stream)
    {
        Span<byte> descriptor = stackalloc byte[ImageDescriptorLength];
        if (!TryReadExactly(stream, descriptor))
        {
            return false;
        }

        if (!Skip(stream, ColorTableByteLength(descriptor[8])))
        {
            return false;
        }

        // The LZW minimum code size byte is followed by the compressed data sub-blocks.
        return stream.ReadByte() >= 0 && SkipDataBlocks(stream);
    }

    private static bool SkipDataBlocks(Stream stream)
    {
        while (true)
        {
            int length = stream.ReadByte();
            if (length < 0)
            {
                return false;
            }

            if (length == BlockTerminator)
            {
                return true;
            }

            if (!Skip(stream, length))
            {
                return false;
            }
        }
    }

    private static int ColorTableByteLength(int packedFlags)
    {
        if ((packedFlags & 0x80) == 0)
        {
            return 0;
        }

        // The table holds 2^(n+1) entries of three bytes each.
        return 3 << ((packedFlags & 0x07) + 1);
    }

    private static bool Skip(Stream stream, int count)
    {
        Span<byte> chunk = stackalloc byte[ReadChunkLength];
        while (count > 0)
        {
            int length = Math.Min(count, ReadChunkLength);
            int read = stream.Read(chunk[..length]);
            if (read != length)
            {
                return false;
            }

            count -= read;
        }

        return true;
    }

    private static bool TryReadExactly(Stream stream, Span<byte> target)
    {
        int offset = 0;
        while (offset < target.Length)
        {
            int read = stream.Read(target[offset..]);
            if (read <= 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
