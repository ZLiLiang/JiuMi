namespace JiuMi.Application.Errors;

/// <summary>
/// Removes the signed-in user's profile path from text shown to the user or logged.
/// </summary>
public static class ErrorRedactor
{
    private static readonly string _userProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Replaces every case variant of the user profile prefix with <c>%USERPROFILE%</c>.
    /// </summary>
    /// <param name="value">The text to redact.</param>
    /// <returns>The text with the profile prefix replaced, or the original text when no profile is known.</returns>
    public static string Redact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return _userProfile.Length == 0
            ? value
            : value.Replace(_userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
    }
}
