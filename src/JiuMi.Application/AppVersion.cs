namespace JiuMi.Application;

/// <summary>
/// Turns assembly version strings into the semantic version shown to users.
/// </summary>
public static class AppVersion
{
    private const string UnknownVersion = "0.0.0";

    /// <summary>
    /// Removes build metadata, everything from the first <c>+</c>, from an
    /// informational version string.
    /// </summary>
    /// <param name="informationalVersion">The assembly informational version.</param>
    /// <returns>The semantic version, or <c>0.0.0</c> when no version is available.</returns>
    public static string FromInformationalVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return UnknownVersion;
        }

        string version = informationalVersion.Trim();
        int metadataStart = version.IndexOf('+', StringComparison.Ordinal);
        if (metadataStart >= 0)
        {
            version = version[..metadataStart];
        }

        return version.Length == 0 ? UnknownVersion : version;
    }
}
