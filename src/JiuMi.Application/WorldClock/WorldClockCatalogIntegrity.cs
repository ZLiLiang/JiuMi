using System;
using System.Security.Cryptography;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Checks downloaded catalog documents against the hashes their manifest lists.
/// </summary>
public static class WorldClockCatalogIntegrity
{
    /// <summary>
    /// Compares the SHA-256 of <paramref name="content"/> with the manifest value.
    /// </summary>
    /// <param name="content">The downloaded bytes.</param>
    /// <param name="expectedSha256">The hexadecimal hash from the manifest.</param>
    /// <returns><see langword="true"/> when the content matches the manifest.</returns>
    public static bool Matches(ReadOnlySpan<byte> content, string expectedSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);

        string actual = Convert.ToHexStringLower(SHA256.HashData(content));
        return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
