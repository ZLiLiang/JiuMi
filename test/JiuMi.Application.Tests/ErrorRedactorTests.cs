using System.Text.Json;
using JiuMi.Application.Errors;

namespace JiuMi.Application.Tests;

public sealed class ErrorRedactorTests
{
    [Fact]
    public void RedactJson_GivenAProfilePathSerializedToJson_ReplacesTheEscapedPrefix()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (userProfile.Length == 0)
        {
            return;
        }

        string json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["path"] = Path.Combine(userProfile, "secret.txt"),
        });

        string redacted = ErrorRedactor.RedactJson(json);

        Assert.Contains("%USERPROFILE%", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(userProfile, redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Redact_GivenAProfilePathInPlainText_ReplacesThePrefix()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (userProfile.Length == 0)
        {
            return;
        }

        string redacted = ErrorRedactor.Redact(Path.Combine(userProfile, "secret.txt"));

        Assert.Equal(Path.Combine("%USERPROFILE%", "secret.txt"), redacted);
    }
}
