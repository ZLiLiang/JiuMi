namespace JiuMi.Application.Errors;

/// <summary>
/// Removes the signed-in user's profile path from text shown to the user or logged.
/// </summary>
public static class ErrorRedactor
{
    private const string Placeholder = "%USERPROFILE%";

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
            : value.Replace(_userProfile, Placeholder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Redacts a JSON document, where backslashes in the profile path are escaped.
    /// </summary>
    /// <param name="json">The serialized text to redact.</param>
    /// <returns>The JSON with both the plain and the escaped profile prefix replaced.</returns>
    public static string RedactJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        string redacted = Redact(json);
        if (_userProfile.Length == 0)
        {
            return redacted;
        }

        string escapedProfile = System.Text.Json.JsonEncodedText.Encode(_userProfile).ToString();
        return string.Equals(escapedProfile, _userProfile, StringComparison.Ordinal)
            ? redacted
            : redacted.Replace(escapedProfile, Placeholder, StringComparison.OrdinalIgnoreCase);
    }
}
