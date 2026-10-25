using System.ComponentModel;
using System.IO.Compression;
using JiuMi.Application.Diagnostics;
using JiuMi.Application.Logging;
using JiuMi.Application.Settings;
using JiuMi.Infrastructure;
using JiuMi.Infrastructure.Diagnostics;
using JiuMi.Infrastructure.Logging;

namespace JiuMi.Infrastructure.Tests;

public sealed class DiagnosticsExporterTests : IDisposable
{
    private static readonly DateTimeOffset _now =
        new(2026, 10, 25, 14, 30, 15, TimeSpan.Zero);

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_GivenLogsCrashSettingsAndSummary_WritesTheFullBundle()
    {
        (SettingsLocation location, string executableDirectory) = CreateExecutableSetup();
        string logsDirectory = Path.Combine(executableDirectory, "logs");
        File.WriteAllText(Path.Combine(logsDirectory, "jiuMi-20261025.jsonl"), "{\"@mt\":\"event\"}");
        File.WriteAllText(Path.Combine(logsDirectory, "crash-20261025-140000.txt"), "crash");
        string settingsPath = Path.Combine(executableDirectory, "config", "settings.json");
        File.WriteAllText(settingsPath, "{\"schemaVersion\":1}");
        DiagnosticsExporter exporter = CreateExporter(location, new StubLoggingStatus("Debug"));
        string destination = Path.Combine(_root, "bundle.zip");

        DiagnosticsExportResult result = await exporter.ExportAsync(destination);

        Assert.True(result.Succeeded, result.Error?.ToString());
        Assert.Equal(Path.GetFullPath(destination), result.DestinationPath);
        Assert.Equal(logsDirectory, exporter.LogDirectory);

        using ZipArchive archive = ZipFile.OpenRead(destination);
        Assert.NotNull(archive.GetEntry("jiuMi-20261025.jsonl"));
        Assert.NotNull(archive.GetEntry("crash-20261025-140000.txt"));
        Assert.NotNull(archive.GetEntry("settings.json"));

        string summary = ReadEntry(archive, "diagnostics.txt");
        Assert.Contains("appVersion: ", summary, StringComparison.Ordinal);
        Assert.Contains("osDescription: ", summary, StringComparison.Ordinal);
        Assert.Contains("configPath: ", summary, StringComparison.Ordinal);
        Assert.Contains("logsPath: ", summary, StringComparison.Ordinal);
        Assert.Contains("language: en-US", summary, StringComparison.Ordinal);
        Assert.Contains("theme: dark", summary, StringComparison.Ordinal);
        Assert.Contains("logLevelOverride: Debug", summary, StringComparison.Ordinal);
        Assert.Contains("generatedAt: 2026-10-25T14:30:15.0000000+00:00", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_GivenTheLiveLogSink_StillReadsTheOpenLogFile()
    {
        (SettingsLocation location, _) = CreateExecutableSetup();
        using var pipeline = new LoggingPipeline(
            new AssemblyApplicationInfo(typeof(LoggingPipeline).Assembly),
            levelOverride: null);
        pipeline.AttachFileLogging(location);
        pipeline.Logger.Information("Live event");
        DiagnosticsExporter exporter = CreateExporter(location, new StubLoggingStatus(null));
        string destination = Path.Combine(_root, "bundle.zip");

        DiagnosticsExportResult result = await exporter.ExportAsync(destination);

        Assert.True(result.Succeeded, result.Error?.ToString());
        using ZipArchive archive = ZipFile.OpenRead(destination);
        Assert.Contains(
            archive.Entries,
            entry => entry.Name.StartsWith("jiuMi-", StringComparison.Ordinal) &&
                entry.Name.EndsWith(".jsonl", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportAsync_GivenAnExistingBundle_ReplacesIt()
    {
        (SettingsLocation location, _) = CreateExecutableSetup();
        DiagnosticsExporter exporter = CreateExporter(location, new StubLoggingStatus(null));
        string destination = Path.Combine(_root, "bundle.zip");
        File.WriteAllText(destination, "old bundle");

        DiagnosticsExportResult result = await exporter.ExportAsync(destination);

        Assert.True(result.Succeeded, result.Error?.ToString());
        using ZipArchive archive = ZipFile.OpenRead(destination);
        Assert.NotNull(archive.GetEntry("diagnostics.txt"));
    }

    [Fact]
    public async Task ExportAsync_GivenMemoryOnlySettings_StillWritesTheSummary()
    {
        Directory.CreateDirectory(_root);
        DiagnosticsExporter exporter = CreateExporter(
            SettingsLocation.MemoryOnly,
            new StubLoggingStatus(null));
        string destination = Path.Combine(_root, "bundle.zip");

        DiagnosticsExportResult result = await exporter.ExportAsync(destination);

        Assert.True(result.Succeeded, result.Error?.ToString());
        using ZipArchive archive = ZipFile.OpenRead(destination);
        Assert.Null(archive.GetEntry("settings.json"));
        string summary = ReadEntry(archive, "diagnostics.txt");
        Assert.Contains("logsPath: unavailable", summary, StringComparison.Ordinal);
        Assert.Contains("logLevelOverride: none", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_GivenAMissingDestinationDirectory_ReturnsFailure()
    {
        Directory.CreateDirectory(_root);
        DiagnosticsExporter exporter = CreateExporter(
            SettingsLocation.MemoryOnly,
            new StubLoggingStatus(null));
        string destination = Path.Combine(_root, "missing", "bundle.zip");

        DiagnosticsExportResult result = await exporter.ExportAsync(destination);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task ExportAsync_GivenTheDestinationIsADirectory_ReturnsFailureAndRemovesTheTemporaryFile()
    {
        Directory.CreateDirectory(_root);
        DiagnosticsExporter exporter = CreateExporter(
            SettingsLocation.MemoryOnly,
            new StubLoggingStatus(null));
        string destination = Path.Combine(_root, "bundle.zip");
        Directory.CreateDirectory(destination);

        DiagnosticsExportResult result = await exporter.ExportAsync(destination);

        Assert.False(result.Succeeded);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void CreateDefaultFileName_GivenTheCurrentTime_UsesTheDocumentedPattern()
    {
        Directory.CreateDirectory(_root);
        DiagnosticsExporter exporter = CreateExporter(
            SettingsLocation.MemoryOnly,
            new StubLoggingStatus(null));

        Assert.Equal(
            "JiuMi-diagnostics-20261025-143015.zip",
            exporter.CreateDefaultFileName());
    }

    private (SettingsLocation Location, string ExecutableDirectory) CreateExecutableSetup()
    {
        string executableDirectory = Path.Combine(_root, "app");
        string configDirectory = Path.Combine(executableDirectory, "config");
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(Path.Combine(executableDirectory, "logs"));
        return (
            SettingsLocation.ForDirectory(SettingsLocationKind.ExecutableDirectory, configDirectory),
            executableDirectory);
    }

    private static DiagnosticsExporter CreateExporter(
        SettingsLocation location,
        ILoggingStatus status) =>
        new(
            new StubSettingsStore(location),
            new AssemblyApplicationInfo(typeof(DiagnosticsExporter).Assembly),
            status,
            new FixedTimeProvider(_now));

    private static string ReadEntry(ZipArchive archive, string name)
    {
        ZipArchiveEntry entry = archive.GetEntry(name)
            ?? throw new InvalidOperationException($"The bundle has no '{name}' entry.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class StubLoggingStatus : ILoggingStatus
    {
        public StubLoggingStatus(string? levelOverride) => LevelOverride = levelOverride;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public bool IsFileLoggingAvailable => true;

        public string? LevelOverride
        {
            get;
        }
    }

    private sealed class StubSettingsStore : ISettingsStore
    {
        public StubSettingsStore(SettingsLocation location) => Location = location;

        public SettingsLocation Location
        {
            get;
        }

        public AppSettings Current
        {
            get;
        } = AppSettings.CreateDefault() with
        {
            Language = "en-US",
            Theme = AppTheme.Dark,
        };

        public SettingsLoadResult Load() => new(Current, Location, SettingsLoadIssue.None);

        public SettingsSaveResult Save(AppSettings settings) => SettingsSaveResult.Success;
    }
}
