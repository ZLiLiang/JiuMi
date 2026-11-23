using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace JiuMi.Application.Updates;

/// <summary>
/// Parses and validates <c>update.json</c> manifests published to GitHub and Gitee.
/// </summary>
public static class AppUpdateManifestJson
{
    /// <summary>
    /// Attempts to parse an <c>update.json</c> document.
    /// </summary>
    /// <param name="json">The raw JSON text.</param>
    /// <param name="manifest">The parsed manifest when successful.</param>
    /// <param name="error">A diagnostic reason when parsing fails.</param>
    /// <returns><see langword="true"/> when the manifest is valid; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(
        string json,
        [NotNullWhen(true)] out AppUpdateManifest? manifest,
        out string? error)
    {
        manifest = null;
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The manifest is empty.";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "The manifest root must be a JSON object.";
                return false;
            }

            if (!TryReadDateTimeOffset(document.RootElement, "generatedAt", out DateTimeOffset generatedAt))
            {
                error = "Missing or invalid generatedAt.";
                return false;
            }

            if (!TryReadRelease(document.RootElement, "stable", out AppUpdateRelease? stable, out error))
            {
                return false;
            }

            if (!TryReadRelease(document.RootElement, "preview", out AppUpdateRelease? preview, out error))
            {
                return false;
            }

            manifest = new AppUpdateManifest(generatedAt, stable, preview);
            return true;
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryReadRelease(
        JsonElement root,
        string propertyName,
        out AppUpdateRelease? release,
        out string? error)
    {
        release = null;
        error = null;

        if (!root.TryGetProperty(propertyName, out JsonElement element) ||
            element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = $"The '{propertyName}' entry must be an object or null.";
            return false;
        }

        if (!TryReadString(element, "version", out string? versionText) ||
            !SemanticVersion.TryParse(versionText, out SemanticVersion? version))
        {
            error = $"The '{propertyName}' entry has a missing or invalid version.";
            return false;
        }

        bool isPrerelease = version.IsPrerelease;
        if (element.TryGetProperty("prerelease", out JsonElement prereleaseElement))
        {
            if (prereleaseElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                error = $"The '{propertyName}' entry has an invalid prerelease flag.";
                return false;
            }

            isPrerelease = prereleaseElement.GetBoolean();
        }

        if (!TryReadDateTimeOffset(element, "publishedAt", out DateTimeOffset publishedAt))
        {
            error = $"The '{propertyName}' entry has a missing or invalid publishedAt.";
            return false;
        }

        if (!TryReadHttpsUri(element, "pageUrl", required: true, out Uri? pageUrl) || pageUrl is null)
        {
            error = $"The '{propertyName}' entry has a missing or invalid HTTPS pageUrl.";
            return false;
        }

        if (!TryReadHttpsUri(element, "assetUrl", required: false, out Uri? assetUrl))
        {
            error = $"The '{propertyName}' entry has an invalid HTTPS assetUrl.";
            return false;
        }

        string? assetSha256 = null;
        if (element.TryGetProperty("assetSha256", out JsonElement shaElement) &&
            shaElement.ValueKind != JsonValueKind.Null)
        {
            if (shaElement.ValueKind != JsonValueKind.String)
            {
                error = $"The '{propertyName}' entry has an invalid assetSha256.";
                return false;
            }

            assetSha256 = shaElement.GetString();
        }

        long assetSize = 0;
        if (element.TryGetProperty("assetSize", out JsonElement sizeElement) &&
            sizeElement.ValueKind != JsonValueKind.Null)
        {
            if (sizeElement.ValueKind != JsonValueKind.Number ||
                !sizeElement.TryGetInt64(out assetSize) ||
                assetSize < 0)
            {
                error = $"The '{propertyName}' entry has an invalid assetSize.";
                return false;
            }
        }

        release = new AppUpdateRelease(
            version,
            isPrerelease,
            publishedAt,
            pageUrl,
            assetUrl,
            assetSha256,
            assetSize);
        return true;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadDateTimeOffset(
        JsonElement element,
        string propertyName,
        out DateTimeOffset value)
    {
        value = default;
        return TryReadString(element, propertyName, out string? text) &&
            DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out value);
    }

    private static bool TryReadHttpsUri(
        JsonElement element,
        string propertyName,
        bool required,
        out Uri? uri)
    {
        uri = null;
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return !required;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return !required;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? parsed) ||
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
