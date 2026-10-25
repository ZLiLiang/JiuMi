using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using JiuMi.Application;
using JiuMi.Application.Diagnostics;
using JiuMi.Application.Errors;
using JiuMi.Application.Logging;
using JiuMi.Application.Settings;
using JiuMi.Infrastructure.Logging;

namespace JiuMi.Infrastructure.Diagnostics;

/// <summary>
/// Packs the current log files, crash files, settings snapshot and environment summary into a zip.
/// </summary>
public sealed class DiagnosticsExporter : IDiagnosticsExporter
{
    private const string SummaryEntryName = "diagnostics.txt";
    private const string SettingsEntryName = "settings.json";

    private readonly ISettingsStore _settingsStore;
    private readonly IApplicationInfo _applicationInfo;
    private readonly ILoggingStatus _loggingStatus;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsExporter"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies the effective paths and the current preference snapshot.</param>
    /// <param name="applicationInfo">Supplies the application version.</param>
    /// <param name="loggingStatus">Supplies the level override shown in the summary.</param>
    /// <param name="timeProvider">Supplies the generated time and the default file name.</param>
    public DiagnosticsExporter(
        ISettingsStore settingsStore,
        IApplicationInfo applicationInfo,
        ILoggingStatus loggingStatus,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(loggingStatus);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _settingsStore = settingsStore;
        _applicationInfo = applicationInfo;
        _loggingStatus = loggingStatus;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public string? LogDirectory => LogPaths.For(_settingsStore.Location);

    /// <inheritdoc />
    public string CreateDefaultFileName() =>
        $"JiuMi-diagnostics-{_timeProvider.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.zip";

    /// <inheritdoc />
    public Task<DiagnosticsExportResult> ExportAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        // ZipFile and ZipArchive are synchronous. The export runs on the thread pool so a large
        // retained log set cannot freeze the settings page; the button disables meanwhile.
        return Task.Run(() => Export(destinationPath, cancellationToken), cancellationToken);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Export is a user-action boundary: every failure must become a result the info bar can report.")]
    private DiagnosticsExportResult Export(string destinationPath, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(destinationPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is null || !Directory.Exists(directory))
        {
            return DiagnosticsExportResult.Failed(
                fullPath,
                new DirectoryNotFoundException($"The destination directory '{directory}' does not exist."));
        }

        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (ZipArchive archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                AddLogFiles(archive, cancellationToken);
                AddSettingsSnapshot(archive);
                AddSummary(archive);
            }

            File.Move(tempPath, fullPath, overwrite: true);
            return DiagnosticsExportResult.Success(fullPath);
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception exception)
        {
            TryDelete(tempPath);
            return DiagnosticsExportResult.Failed(fullPath, exception);
        }
    }

    private void AddLogFiles(ZipArchive archive, CancellationToken cancellationToken)
    {
        string? logsDirectory = LogDirectory;
        if (logsDirectory is null || !Directory.Exists(logsDirectory))
        {
            return;
        }

        IEnumerable<string> files = Directory
            .EnumerateFiles(logsDirectory, "*.jsonl")
            .Concat(Directory.EnumerateFiles(logsDirectory, "crash-*.txt"))
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddFile(archive, file, Path.GetFileName(file));
        }
    }

    private void AddSettingsSnapshot(ZipArchive archive)
    {
        string? settingsPath = _settingsStore.Location.FilePath;
        if (settingsPath is not null && File.Exists(settingsPath))
        {
            AddFile(archive, settingsPath, SettingsEntryName);
        }
    }

    private void AddSummary(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.CreateEntry(SummaryEntryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        WriteField(writer, "appVersion", _applicationInfo.Version);
        WriteField(writer, "osDescription", RuntimeInformation.OSDescription);
        WriteField(writer, "configPath", _settingsStore.Location.FilePath ?? "unavailable");
        WriteField(writer, "logsPath", LogDirectory ?? "unavailable");
        WriteField(writer, "language", _settingsStore.Current.Language ?? "unspecified");
        WriteField(writer, "theme", ToStorageValue(_settingsStore.Current.Theme));
        WriteField(writer, "logLevelOverride", _loggingStatus.LevelOverride ?? "none");
        WriteField(
            writer,
            "generatedAt",
            _timeProvider.GetLocalNow().ToString("O", CultureInfo.InvariantCulture));
    }

    private static void WriteField(TextWriter writer, string key, string value) =>
        writer.WriteLine($"{key}: {ErrorRedactor.Redact(value)}");

    private static void AddFile(ZipArchive archive, string path, string entryName)
    {
        using var source = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using Stream target = entry.Open();
        source.CopyTo(target);
    }

    private static string ToStorageValue(AppTheme theme) =>
        theme switch
        {
            AppTheme.Light => "light",
            AppTheme.Dark => "dark",
            _ => "system",
        };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
