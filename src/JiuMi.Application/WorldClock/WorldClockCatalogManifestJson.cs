using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// Parses and validates the manifest a catalog release publishes.
/// </summary>
public static class WorldClockCatalogManifestJson
{
    /// <summary>
    /// Parses a manifest document, rejecting anything the application cannot trust.
    /// </summary>
    /// <param name="json">The manifest text.</param>
    /// <param name="manifest">The parsed manifest, when valid.</param>
    /// <param name="error">Why the document was rejected, when it was.</param>
    /// <returns><see langword="true"/> when the manifest is well formed and complete.</returns>
    public static bool TryParse(
        string json,
        out WorldClockCatalogManifest? manifest,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(json);

        manifest = null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The manifest is not a JSON object.";
                return false;
            }

            if (!TryReadInt(root, "schemaVersion", out int schemaVersion) ||
                schemaVersion != WorldClockCatalogManifest.CurrentSchemaVersion)
            {
                error = "The manifest schema version is not supported.";
                return false;
            }

            if (!TryReadInt(root, "version", out int version) || version < 1)
            {
                error = "The manifest version must be a positive number.";
                return false;
            }

            if (!root.TryGetProperty("updatedAt", out JsonElement updatedAtElement) ||
                updatedAtElement.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(
                    updatedAtElement.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset updatedAt))
            {
                error = "The manifest updatedAt is not a timestamp.";
                return false;
            }

            if (!TryReadFiles(root, out IReadOnlyList<WorldClockCatalogManifestFile>? files, out error) ||
                files is null)
            {
                return false;
            }

            manifest = new WorldClockCatalogManifest(schemaVersion, version, updatedAt, files);
            error = null;
            return true;
        }
        catch (JsonException exception)
        {
            error = "The manifest is not valid JSON: " + exception.Message;
            return false;
        }
    }

    private static bool TryReadFiles(
        JsonElement root,
        out IReadOnlyList<WorldClockCatalogManifestFile>? files,
        out string? error)
    {
        files = null;
        if (!root.TryGetProperty("files", out JsonElement array) || array.ValueKind != JsonValueKind.Array)
        {
            error = "The manifest has no files array.";
            return false;
        }

        var parsed = new List<WorldClockCatalogManifestFile>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !TryReadString(element, "name", out string? name) ||
                !TryReadString(element, "sha256", out string? sha256) ||
                name is null ||
                sha256 is null ||
                !TryReadLong(element, "size", out long size))
            {
                error = "A manifest file entry is incomplete.";
                return false;
            }

            if (!WorldClockCatalogManifest.RequiredFiles.Contains(name, StringComparer.Ordinal))
            {
                error = $"The manifest lists an unexpected file '{name}'.";
                return false;
            }

            if (!names.Add(name))
            {
                error = $"The manifest lists '{name}' twice.";
                return false;
            }

            if (sha256.Length != 64 || !sha256.All(char.IsAsciiHexDigit))
            {
                error = $"The manifest hash of '{name}' is not a SHA-256 value.";
                return false;
            }

            if (size <= 0)
            {
                error = $"The manifest size of '{name}' must be positive.";
                return false;
            }

            parsed.Add(new WorldClockCatalogManifestFile(name, sha256, size));
        }

        foreach (string required in WorldClockCatalogManifest.RequiredFiles)
        {
            if (!names.Contains(required))
            {
                error = $"The manifest does not list '{required}'.";
                return false;
            }
        }

        files = parsed;
        error = null;
        return true;
    }

    private static bool TryReadInt(JsonElement element, string property, out int value)
    {
        value = 0;
        return element.TryGetProperty(property, out JsonElement propertyElement) &&
            propertyElement.ValueKind == JsonValueKind.Number &&
            propertyElement.TryGetInt32(out value);
    }

    private static bool TryReadLong(JsonElement element, string property, out long value)
    {
        value = 0;
        return element.TryGetProperty(property, out JsonElement propertyElement) &&
            propertyElement.ValueKind == JsonValueKind.Number &&
            propertyElement.TryGetInt64(out value);
    }

    private static bool TryReadString(JsonElement element, string property, out string? value)
    {
        value = element.TryGetProperty(property, out JsonElement propertyElement) &&
            propertyElement.ValueKind == JsonValueKind.String
            ? propertyElement.GetString()?.Trim()
            : null;
        return !string.IsNullOrEmpty(value);
    }
}
