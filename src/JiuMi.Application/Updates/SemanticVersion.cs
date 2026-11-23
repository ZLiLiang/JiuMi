using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace JiuMi.Application.Updates;

/// <summary>
/// A Semantic Versioning 2.0.0 version with full prerelease precedence ordering.
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private readonly string _canonical;

    private SemanticVersion(int major, int minor, int patch, IReadOnlyList<string> prereleaseIdentifiers)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PrereleaseIdentifiers = prereleaseIdentifiers;
        _canonical = prereleaseIdentifiers.Count == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{major}.{minor}.{patch}-{string.Join('.', prereleaseIdentifiers)}");
    }

    /// <summary>
    /// Gets the major version component.
    /// </summary>
    public int Major
    {
        get;
    }

    /// <summary>
    /// Gets the minor version component.
    /// </summary>
    public int Minor
    {
        get;
    }

    /// <summary>
    /// Gets the patch version component.
    /// </summary>
    public int Patch
    {
        get;
    }

    /// <summary>
    /// Gets the dot-separated prerelease identifiers, or an empty list for a stable version.
    /// </summary>
    public IReadOnlyList<string> PrereleaseIdentifiers
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether this version carries a prerelease label.
    /// </summary>
    public bool IsPrerelease => PrereleaseIdentifiers.Count > 0;

    /// <summary>
    /// Parses a Semantic Version 2.0.0 string, ignoring any build metadata.
    /// </summary>
    /// <param name="text">The version text to parse.</param>
    /// <returns>The parsed semantic version.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="text"/> is not a valid SemVer 2.0.0 string.</exception>
    public static SemanticVersion Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryParse(text, out SemanticVersion? version))
        {
            throw new FormatException($"'{text}' is not a valid semantic version.");
        }

        return version;
    }

    /// <summary>
    /// Attempts to parse a Semantic Version 2.0.0 string, ignoring any build metadata.
    /// </summary>
    /// <param name="text">The version text to parse.</param>
    /// <param name="version">The parsed semantic version when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        string coreAndPrerelease = trimmed;
        int buildIndex = trimmed.IndexOf('+', StringComparison.Ordinal);
        if (buildIndex >= 0)
        {
            string build = trimmed[(buildIndex + 1)..];
            if (!IsValidBuildMetadata(build))
            {
                return false;
            }

            coreAndPrerelease = trimmed[..buildIndex];
        }

        string core = coreAndPrerelease;
        IReadOnlyList<string> prerelease = [];
        int prereleaseIndex = coreAndPrerelease.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            string prereleaseText = coreAndPrerelease[(prereleaseIndex + 1)..];
            if (!TryParsePrerelease(prereleaseText, out IReadOnlyList<string>? parsedPrerelease))
            {
                return false;
            }

            prerelease = parsedPrerelease;
            core = coreAndPrerelease[..prereleaseIndex];
        }

        string[] parts = core.Split('.');
        if (parts.Length != 3 ||
            !TryParseCoreComponent(parts[0], out int major) ||
            !TryParseCoreComponent(parts[1], out int minor) ||
            !TryParseCoreComponent(parts[2], out int patch))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int comparison = Major.CompareTo(other.Major);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0)
        {
            return comparison;
        }

        if (!IsPrerelease && !other.IsPrerelease)
        {
            return 0;
        }

        if (!IsPrerelease)
        {
            return 1;
        }

        if (!other.IsPrerelease)
        {
            return -1;
        }

        int count = Math.Min(PrereleaseIdentifiers.Count, other.PrereleaseIdentifiers.Count);
        for (int index = 0; index < count; index++)
        {
            comparison = ComparePrereleaseIdentifier(
                PrereleaseIdentifiers[index],
                other.PrereleaseIdentifiers[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return PrereleaseIdentifiers.Count.CompareTo(other.PrereleaseIdentifiers.Count);
    }

    /// <inheritdoc />
    public bool Equals(SemanticVersion? other) =>
        other is not null && string.Equals(_canonical, other._canonical, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_canonical);

    /// <inheritdoc />
    public override string ToString() => _canonical;

    /// <summary>
    /// Compares two semantic versions for equality.
    /// </summary>
    public static bool operator ==(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Compares two semantic versions for inequality.
    /// </summary>
    public static bool operator !=(SemanticVersion? left, SemanticVersion? right) => !(left == right);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="left"/> has lower precedence than <paramref name="right"/>.
    /// </summary>
    public static bool operator <(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="left"/> has lower or equal precedence to <paramref name="right"/>.
    /// </summary>
    public static bool operator <=(SemanticVersion? left, SemanticVersion? right) =>
        left is null || left.CompareTo(right) <= 0;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="left"/> has higher precedence than <paramref name="right"/>.
    /// </summary>
    public static bool operator >(SemanticVersion? left, SemanticVersion? right) =>
        left is not null && left.CompareTo(right) > 0;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="left"/> has higher or equal precedence to <paramref name="right"/>.
    /// </summary>
    public static bool operator >=(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    private static bool TryParseCoreComponent(string text, out int value)
    {
        value = 0;
        if (!IsNumericIdentifier(text) || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParsePrerelease(
        string text,
        [NotNullWhen(true)] out IReadOnlyList<string>? identifiers)
    {
        identifiers = null;
        if (!TrySplitAsciiIdentifiers(text, out string[]? parts))
        {
            return false;
        }

        foreach (string part in parts)
        {
            if (IsNumericIdentifier(part) && part.Length > 1 && part[0] == '0')
            {
                return false;
            }
        }

        identifiers = parts;
        return true;
    }

    private static bool IsValidBuildMetadata(string text) => TrySplitAsciiIdentifiers(text, out _);

    private static bool TrySplitAsciiIdentifiers(string text, [NotNullWhen(true)] out string[]? parts)
    {
        parts = null;
        if (text.Length == 0)
        {
            return false;
        }

        string[] split = text.Split('.');
        foreach (string part in split)
        {
            if (part.Length == 0 || !IsAsciiIdentifier(part))
            {
                return false;
            }
        }

        parts = split;
        return true;
    }

    private static bool IsAsciiIdentifier(string text)
    {
        foreach (char character in text)
        {
            bool valid = character is (>= '0' and <= '9')
                or (>= 'A' and <= 'Z')
                or (>= 'a' and <= 'z')
                or '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNumericIdentifier(string text)
    {
        foreach (char character in text)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return text.Length > 0;
    }

    private static int ComparePrereleaseIdentifier(string left, string right)
    {
        bool leftNumeric = IsNumericIdentifier(left);
        bool rightNumeric = IsNumericIdentifier(right);

        if (leftNumeric && rightNumeric)
        {
            int lengthComparison = left.Length.CompareTo(right.Length);
            return lengthComparison != 0
                ? lengthComparison
                : string.CompareOrdinal(left, right);
        }

        if (leftNumeric)
        {
            return -1;
        }

        if (rightNumeric)
        {
            return 1;
        }

        return string.CompareOrdinal(left, right);
    }
}
