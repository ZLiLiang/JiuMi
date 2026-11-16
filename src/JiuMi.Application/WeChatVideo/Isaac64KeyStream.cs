using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Generates the ISAAC64 key stream that decrypts the head of a WeChat video. The seed is the
/// item's <c>decode_key</c>; the output matches the legacy implementation, which consumes the
/// random buffer backwards and writes each 64-bit word big-endian.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The ticket names the ISAAC64 key stream; it is not a System.IO.Stream.")]
public sealed class Isaac64KeyStream
{
    private const int WordCount = 256;
    private const ulong IndexMask = 0x7F8;

    private static readonly ulong[] _magic =
    [
        0x647C4677A2884B7C,
        0xB9F8B322C73AC862,
        0x8C0EA5053D4712A0,
        0xB29B2E824A595524,
        0x82F053DB8355E0CE,
        0x48FE4A0FA5A09315,
        0xAE985BF2CBFC89ED,
        0x98F5704F6C44C0AB,
    ];

    private readonly ulong[] _state = new ulong[WordCount];
    private readonly ulong[] _results = new ulong[WordCount];
    private ulong _a;
    private ulong _b;
    private ulong _c;
    private int _cursor = WordCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="Isaac64KeyStream"/> class.
    /// </summary>
    /// <param name="seed">The numeric <c>decode_key</c>.</param>
    public Isaac64KeyStream(ulong seed)
    {
        _state[0] = seed;
        Initialize();
    }

    /// <summary>
    /// Creates the XOR mask for the first bytes of an encrypted video.
    /// </summary>
    /// <param name="length">The number of bytes to generate.</param>
    /// <returns>The key stream bytes.</returns>
    public byte[] CreateDecryptionArray(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        byte[] stream = new byte[length];
        Span<byte> block = stackalloc byte[8];
        for (int offset = 0; offset < length; offset += 8)
        {
            BinaryPrimitives.WriteUInt64BigEndian(block, NextWord());
            block[..Math.Min(block.Length, length - offset)].CopyTo(stream.AsSpan(offset));
        }

        return stream;
    }

    private void Initialize()
    {
        Span<ulong> seed = stackalloc ulong[8];
        _magic.CopyTo(seed);

        for (int i = 0; i < WordCount; i += 8)
        {
            for (int j = 0; j < 8; j++)
            {
                seed[j] += _state[i + j];
            }

            Mix(seed);
            seed.CopyTo(_state.AsSpan(i));
        }

        for (int i = 0; i < WordCount; i += 8)
        {
            for (int j = 0; j < 8; j++)
            {
                seed[j] += _state[i + j];
            }

            Mix(seed);
            seed.CopyTo(_state.AsSpan(i));
        }

        Generate();
        _cursor = WordCount;
    }

    private void Generate()
    {
        ulong a = _a;
        _c++;
        ulong b = _b + _c;
        int other = WordCount / 2;

        for (int index = 0; index < WordCount / 2; index++)
        {
            Step(index, other++, ref a, ref b);
        }

        other = 0;
        for (int index = WordCount / 2; index < WordCount; index++)
        {
            Step(index, other++, ref a, ref b);
        }

        _a = a;
        _b = b;
    }

    private void Step(int index, int otherIndex, ref ulong a, ref ulong b)
    {
        ulong x = _state[index];
        a = (index % 4) switch
        {
            0 => ~(a ^ (a << 21)) + _state[otherIndex],
            1 => (a ^ (a >> 5)) + _state[otherIndex],
            2 => (a ^ (a << 12)) + _state[otherIndex],
            _ => (a ^ (a >> 33)) + _state[otherIndex],
        };

        ulong y = _state[(int)(x & IndexMask) >> 3] + a + b;
        _state[index] = y;
        b = _state[(int)((y >> 8) & IndexMask) >> 3] + x;
        _results[index] = b;
    }

    private ulong NextWord()
    {
        _cursor--;
        if (_cursor < 0)
        {
            Generate();
            _cursor = WordCount - 1;
        }

        return _results[_cursor];
    }

    private static void Mix(Span<ulong> x)
    {
        x[0] -= x[4];
        x[5] ^= x[7] >> 9;
        x[7] += x[0];
        x[1] -= x[5];
        x[6] ^= x[0] << 9;
        x[0] += x[1];
        x[2] -= x[6];
        x[7] ^= x[1] >> 23;
        x[1] += x[2];
        x[3] -= x[7];
        x[0] ^= x[2] << 15;
        x[2] += x[3];
        x[4] -= x[0];
        x[1] ^= x[3] >> 14;
        x[3] += x[4];
        x[5] -= x[1];
        x[2] ^= x[4] << 20;
        x[4] += x[5];
        x[6] -= x[2];
        x[3] ^= x[5] >> 17;
        x[5] += x[6];
        x[7] -= x[3];
        x[4] ^= x[6] << 14;
        x[6] += x[7];
    }
}
