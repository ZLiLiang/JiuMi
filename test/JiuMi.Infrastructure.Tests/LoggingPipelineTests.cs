using System.Text.Json;
using JiuMi.Application.Errors;
using JiuMi.Application.Settings;
using JiuMi.Infrastructure;
using JiuMi.Infrastructure.Logging;
using Serilog.Events;

namespace JiuMi.Infrastructure.Tests;

public sealed class LoggingPipelineTests : IDisposable
{
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
    public void AttachFileLogging_GivenExecutableLocation_WritesSiblingLogsWithAmbientFields()
    {
        string executableDirectory = Path.Combine(_root, "app");
        Directory.CreateDirectory(executableDirectory);
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(executableDirectory, "config"));

        using (var pipeline = CreatePipeline())
        {
            pipeline.AttachFileLogging(location);
            pipeline.Logger.Information("Probe {Kind}", "executable");
        }

        string logsDirectory = Path.Combine(executableDirectory, "logs");
        string logFile = Assert.Single(Directory.GetFiles(logsDirectory, "jiuMi-*.jsonl"));
        Assert.Matches(@"^jiuMi-\d{8}\.jsonl$", Path.GetFileName(logFile));
        JsonElement entry = Assert.Single(
            ReadEntries(logFile),
            candidate => candidate.GetProperty("@mt").GetString() == "Probe {Kind}");
        Assert.EndsWith(
            "Z",
            entry.GetProperty("@t").GetString() ?? string.Empty,
            StringComparison.Ordinal);

        Assert.Equal("Probe {Kind}", entry.GetProperty("@mt").GetString());
        Assert.Equal("executable", entry.GetProperty("Kind").GetString());
        Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("appVersion").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("osDescription").GetString()));
        Assert.Equal(
            ErrorRedactor.Redact(location.FilePath!),
            entry.GetProperty("configPath").GetString());
    }

    [Fact]
    public void AttachFileLogging_GivenAppDataLocation_WritesIntoTheAppDataDirectory()
    {
        string appDataDirectory = Path.Combine(_root, "appdata", "JiuMi");
        Directory.CreateDirectory(appDataDirectory);
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.AppData,
            appDataDirectory);

        using (var pipeline = CreatePipeline())
        {
            pipeline.AttachFileLogging(location);
            pipeline.Logger.Information("AppData probe");
        }

        Assert.Single(Directory.GetFiles(Path.Combine(appDataDirectory, "logs"), "jiuMi-*.jsonl"));
    }

    [Fact]
    public void AttachFileLogging_GivenMemoryOnly_ReportsUnavailableAndKeepsLoggingSafe()
    {
        using var pipeline = CreatePipeline();

        pipeline.AttachFileLogging(SettingsLocation.MemoryOnly);

        Assert.False(pipeline.Status.IsFileLoggingAvailable);
        pipeline.Logger.Information("No file sink");
        Assert.False(Directory.Exists(Path.Combine(_root, "logs")));
    }

    [Fact]
    public void AttachFileLogging_GivenEventsLoggedBeforeTheProbe_ReplaysThemInOrder()
    {
        string executableDirectory = Path.Combine(_root, "app");
        Directory.CreateDirectory(executableDirectory);
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(executableDirectory, "config"));

        using (var pipeline = CreatePipeline())
        {
            pipeline.Logger.Information("Before the probe");
            pipeline.AttachFileLogging(location);
            pipeline.Logger.Information("After the probe");
        }

        string logFile = Assert.Single(
            Directory.GetFiles(Path.Combine(executableDirectory, "logs"), "jiuMi-*.jsonl"));
        string[] messages = ReadEntries(logFile)
            .Select(entry => entry.GetProperty("@mt").GetString() ?? string.Empty)
            .Where(message => !message.StartsWith("File logging started", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(["Before the probe", "After the probe"], messages);
    }

    [Fact]
    public void Logger_GivenAPathUnderTheUserProfile_RedactsIt()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (userProfile.Length == 0)
        {
            return;
        }

        string executableDirectory = Path.Combine(_root, "app");
        Directory.CreateDirectory(executableDirectory);
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(executableDirectory, "config"));

        using (var pipeline = CreatePipeline())
        {
            pipeline.AttachFileLogging(location);
            pipeline.Logger.Information("Reading {Path}", Path.Combine(userProfile, "secret.txt"));
        }

        string logFile = Assert.Single(
            Directory.GetFiles(Path.Combine(executableDirectory, "logs"), "jiuMi-*.jsonl"));
        string json = File.ReadAllText(logFile);

        Assert.Contains("%USERPROFILE%", json, StringComparison.Ordinal);
        Assert.DoesNotContain(userProfile, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Logger_GivenAnInvalidLevelOverride_ReplaysASelfDiagnosticWarning()
    {
        string executableDirectory = Path.Combine(_root, "app");
        Directory.CreateDirectory(executableDirectory);
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(executableDirectory, "config"));

        string? levelOverride;
        using (var pipeline = CreatePipeline("Trace"))
        {
            pipeline.AttachFileLogging(location);
            pipeline.Logger.Information("Normal event");
            levelOverride = pipeline.Status.LevelOverride;
        }

        string logFile = Assert.Single(
            Directory.GetFiles(Path.Combine(executableDirectory, "logs"), "jiuMi-*.jsonl"));
        JsonElement[] entries = ReadEntries(logFile);

        Assert.Equal("Trace", levelOverride);
        JsonElement warning = Assert.Single(
            entries,
            entry => entry.TryGetProperty("@l", out JsonElement level) &&
                level.GetString() == nameof(LogEventLevel.Warning));
        Assert.Contains("Invalid", warning.GetProperty("@mt").GetString(), StringComparison.Ordinal);
        Assert.Equal("Trace", warning.GetProperty("Value").GetString());
        Assert.Contains(
            entries,
            entry => entry.GetProperty("@mt").GetString() == "Normal event");
    }

    [Fact]
    public void Logger_GivenDebugOverride_WritesDebugEvents()
    {
        string executableDirectory = Path.Combine(_root, "app");
        Directory.CreateDirectory(executableDirectory);
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(executableDirectory, "config"));

        using (var pipeline = CreatePipeline("Debug"))
        {
            pipeline.AttachFileLogging(location);
            pipeline.Logger.Debug("Detail event");
        }

        string logFile = Assert.Single(
            Directory.GetFiles(Path.Combine(executableDirectory, "logs"), "jiuMi-*.jsonl"));
        Assert.Contains(
            ReadEntries(logFile),
            entry => entry.GetProperty("@mt").GetString() == "Detail event");
    }

    [Fact]
    public void AttachFileLogging_GivenASinkFailureReportedBySerilog_MarksFileLoggingUnavailable()
    {
        string executableDirectory = Path.Combine(_root, "app");
        Directory.CreateDirectory(executableDirectory);
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(executableDirectory, "config"));

        using var pipeline = CreatePipeline();
        pipeline.AttachFileLogging(location);
        Assert.True(pipeline.Status.IsFileLoggingAvailable);

        Serilog.Debugging.SelfLog.WriteLine(
            "Caught exception while emitting to sink Example: disk is full");

        Assert.False(pipeline.Status.IsFileLoggingAvailable);
    }

    private static LoggingPipeline CreatePipeline(string? levelOverride = null) =>
        new(
            new AssemblyApplicationInfo(typeof(LoggingPipeline).Assembly),
            levelOverride);

    private static JsonElement[] ReadEntries(string logFile) =>
        File.ReadAllLines(logFile)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
}
