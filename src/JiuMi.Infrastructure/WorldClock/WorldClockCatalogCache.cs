using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.WorldClock;

/// <summary>
/// Stores verified catalog versions beside the settings file and reads the newest usable one back.
/// </summary>
/// <remarks>
/// Each version is one directory, moved into place only after every file was written, so a reader never
/// sees a half-written catalog. Only the newest version is kept; a failed write leaves the previous one.
/// </remarks>
internal sealed class WorldClockCatalogCache
{
    private const string ManifestFileName = "manifest.json";
    private const string StateFileName = "state.json";
    private const string TemporaryDirectoryPrefix = ".tmp-";

    private static readonly JsonSerializerOptions _documentOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string? _directory;
    private readonly Serilog.ILogger _logger;

    public WorldClockCatalogCache(string? settingsDirectory, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _directory = settingsDirectory is null
            ? null
            : Path.Combine(settingsDirectory, "cache", "catalog");
        _logger = logger;
    }

    public bool IsAvailable => _directory is not null;

    public int ReadLatestVersion()
    {
        if (_directory is null || !Directory.Exists(_directory))
        {
            return WorldClockCatalogVersion.Bundled;
        }

        try
        {
            int latest = WorldClockCatalogVersion.Bundled;
            foreach (string path in Directory.EnumerateDirectories(_directory))
            {
                if (int.TryParse(
                        Path.GetFileName(path),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int version) &&
                    version > latest)
                {
                    latest = version;
                }
            }

            return latest;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Debug(exception, "Catalog cache versions could not be listed");
            return WorldClockCatalogVersion.Bundled;
        }
    }

    public async Task<InstalledCatalogVersion?> TryReadInstalledAsync(CancellationToken cancellationToken)
    {
        int latest = ReadLatestVersion();
        if (!TryReadManifest(latest, out WorldClockCatalogManifest? manifest) || manifest is null)
        {
            return null;
        }

        // A directory that no longer parses is not an installed catalog; the updater may replace it.
        WorldClockCatalog? catalog = await LoadAsync(latest, cancellationToken).ConfigureAwait(false);
        return catalog is not null ? new InstalledCatalogVersion(latest, manifest.UpdatedAt) : null;
    }

    /// <summary>
    /// Reads the newest manifest without verifying the files, for display only.
    /// </summary>
    public bool TryReadManifestVersion(out int version, out DateTimeOffset? updatedAt)
    {
        version = ReadLatestVersion();
        updatedAt = null;
        if (!TryReadManifest(version, out WorldClockCatalogManifest? manifest) || manifest is null)
        {
            version = WorldClockCatalogVersion.Bundled;
            return false;
        }

        updatedAt = manifest.UpdatedAt;
        return true;
    }

    public async Task<WorldClockCatalog?> LoadAsync(int version, CancellationToken cancellationToken)
    {
        string? directory = GetVersionDirectory(version);
        if (directory is null ||
            !TryReadManifest(version, out WorldClockCatalogManifest? manifest) ||
            manifest is null)
        {
            return null;
        }

        var documents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (WorldClockCatalogManifestFile file in manifest.Files)
        {
            string path = Path.Combine(directory, file.Name);
            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsFileFailure(exception))
            {
                _logger.Warning(exception, "Catalog cache file {FileName} could not be read", file.Name);
                return null;
            }

            if (content.LongLength != file.Size || !WorldClockCatalogIntegrity.Matches(content, file.Sha256))
            {
                _logger.Warning("Catalog cache file {FileName} does not match its manifest", file.Name);
                return null;
            }

            documents[file.Name] = WorldClockCatalogText.Decode(content);
        }

        if (!WorldClockCatalogJson.TryParse(
                documents["countries.json"],
                documents["cities.json"],
                documents["office-hours.json"],
                out WorldClockCatalog? catalog,
                out IReadOnlyList<string> issues) ||
            catalog is null)
        {
            _logger.Warning("Catalog cache version {Version} could not be parsed", version);
            return null;
        }

        if (issues.Count > 0)
        {
            _logger.Debug(
                "Catalog cache version {Version} contains {IssueCount} data issues",
                version,
                issues.Count);
        }

        return catalog;
    }

    public bool TryReadManifest(int version, out WorldClockCatalogManifest? manifest)
    {
        manifest = null;
        string? directory = GetVersionDirectory(version);
        if (directory is null)
        {
            return false;
        }

        try
        {
            string path = Path.Combine(directory, ManifestFileName);
            return File.Exists(path) &&
                WorldClockCatalogManifestJson.TryParse(File.ReadAllText(path), out manifest, out _) &&
                manifest is not null &&
                manifest.Version == version;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Debug(exception, "Catalog cache manifest of version {Version} could not be read", version);
            return false;
        }
    }

    public bool Write(
        WorldClockCatalogManifest manifest,
        IReadOnlyDictionary<string, byte[]> files)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(files);

        string? maybeDirectory = GetVersionDirectory(manifest.Version);
        if (_directory is null || maybeDirectory is null)
        {
            return false;
        }

        string directory = maybeDirectory;
        string temporary = Path.Combine(
            _directory,
            TemporaryDirectoryPrefix + manifest.Version.ToString(CultureInfo.InvariantCulture));
        try
        {
            DeleteDirectory(temporary);
            Directory.CreateDirectory(temporary);
            foreach (WorldClockCatalogManifestFile file in manifest.Files)
            {
                if (!files.TryGetValue(file.Name, out byte[]? content))
                {
                    _logger.Warning("Catalog version {Version} has no {FileName}", manifest.Version, file.Name);
                    TryDeleteDirectory(temporary);
                    return false;
                }

                File.WriteAllBytes(Path.Combine(temporary, file.Name), content);
            }

            File.WriteAllText(
                Path.Combine(temporary, ManifestFileName),
                JsonSerializer.Serialize(ToDocument(manifest), _documentOptions));

            // A same-version directory only survives here when it is unusable (the updater compares
            // versions before downloading) or left over from a crash; replace it instead of trusting it.
            if (Directory.Exists(directory))
            {
                DeleteDirectory(directory);
            }

            Directory.Move(temporary, directory);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Warning(exception, "Catalog version {Version} could not be cached", manifest.Version);
            TryDeleteDirectory(temporary);
            return false;
        }

        PruneOtherVersions(manifest.Version);
        return true;
    }

    public DateTimeOffset? ReadLastCheckedAt()
    {
        if (_directory is null)
        {
            return null;
        }

        try
        {
            string path = Path.Combine(_directory, StateFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("lastCheckedAt", out JsonElement element) &&
                element.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(
                    element.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset lastCheckedAt)
                ? lastCheckedAt
                : null;
        }
        catch (Exception exception) when (IsFileFailure(exception) || exception is JsonException)
        {
            _logger.Debug(exception, "Catalog update state could not be read");
            return null;
        }
    }

    public void WriteLastCheckedAt(DateTimeOffset lastCheckedAt)
    {
        if (_directory is null)
        {
            return;
        }

        string path = Path.Combine(_directory, StateFileName);
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    new StateDocument
                    {
                        LastCheckedAt = lastCheckedAt.ToString("O", CultureInfo.InvariantCulture),
                    },
                    _documentOptions));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Debug(exception, "Catalog update state could not be written");
            TryDeleteFile(temporary);
        }
    }

    private void PruneOtherVersions(int keepVersion)
    {
        if (_directory is null)
        {
            return;
        }

        try
        {
            foreach (string path in Directory.EnumerateDirectories(_directory))
            {
                string name = Path.GetFileName(path);
                bool isTemporary = name.StartsWith(TemporaryDirectoryPrefix, StringComparison.Ordinal);
                bool isOtherVersion = int.TryParse(
                        name,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int version) &&
                    version != keepVersion;
                if (isTemporary || isOtherVersion)
                {
                    DeleteDirectory(path);
                }
            }
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Debug(exception, "Old catalog versions could not be removed");
        }
    }

    private string? GetVersionDirectory(int version)
    {
        if (_directory is null || version < 1)
        {
            return null;
        }

        return Path.Combine(_directory, version.ToString(CultureInfo.InvariantCulture));
    }

    private static ManifestDocument ToDocument(WorldClockCatalogManifest manifest) => new()
    {
        SchemaVersion = manifest.SchemaVersion,
        Version = manifest.Version,
        UpdatedAt = manifest.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
        Files =
        [
            .. manifest.Files.Select(file => new FileDocument
            {
                Name = file.Name,
                Sha256 = file.Sha256,
                Size = file.Size,
            }),
        ],
    };

    private static bool IsFileFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Security.SecurityException;

    private void TryDeleteDirectory(string path)
    {
        try
        {
            DeleteDirectory(path);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Debug(exception, "Catalog temporary directory could not be removed");
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Debug(exception, "Catalog temporary file could not be removed");
        }
    }

    private sealed class ManifestDocument
    {
        public int SchemaVersion
        {
            get;
            init;
        }

        public int Version
        {
            get;
            init;
        }

        public string? UpdatedAt
        {
            get;
            init;
        }

        public List<FileDocument>? Files
        {
            get;
            init;
        }
    }

    private sealed class FileDocument
    {
        public string? Name
        {
            get;
            init;
        }

        public string? Sha256
        {
            get;
            init;
        }

        public long Size
        {
            get;
            init;
        }
    }

    private sealed class StateDocument
    {
        public string? LastCheckedAt
        {
            get;
            init;
        }
    }
}
