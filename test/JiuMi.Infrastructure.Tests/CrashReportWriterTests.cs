using JiuMi.Application;
using JiuMi.Application.Settings;
using JiuMi.Infrastructure;
using JiuMi.Infrastructure.Diagnostics;

namespace JiuMi.Infrastructure.Tests;

public sealed class CrashReportWriterTests : IDisposable
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
    public void Write_GivenAnException_WritesTheCrashFieldsIntoTheLogDirectory()
    {
        string executableDirectory = Path.Combine(_root, "app");
        Directory.CreateDirectory(executableDirectory);
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(executableDirectory, "config"));
        CrashReportWriter writer = CreateWriter(location);

        writer.Write("AppDomain.UnhandledException", CreateException());

        string crashFile = Assert.Single(
            Directory.GetFiles(Path.Combine(executableDirectory, "logs"), "crash-*.txt"));
        string content = File.ReadAllText(crashFile);

        Assert.Contains("timestamp: 2026-10-25T14:30:15.0000000+00:00", content, StringComparison.Ordinal);
        Assert.Contains("appVersion: ", content, StringComparison.Ordinal);
        Assert.Contains("operation: AppDomain.UnhandledException", content, StringComparison.Ordinal);
        Assert.Contains("exceptionType: System.InvalidOperationException", content, StringComparison.Ordinal);
        Assert.Contains("exceptionMessage: The shell could not continue.", content, StringComparison.Ordinal);
        Assert.Contains("CrashReportWriterTests.CreateException", content, StringComparison.Ordinal);
        Assert.Contains("configPath: ", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_GivenMoreThanFiveExistingCrashFiles_KeepsTheNewestFive()
    {
        string executableDirectory = Path.Combine(_root, "app");
        string logsDirectory = Path.Combine(executableDirectory, "logs");
        Directory.CreateDirectory(logsDirectory);
        for (int day = 1; day <= 5; day++)
        {
            File.WriteAllText(
                Path.Combine(logsDirectory, $"crash-2026010{day}-000000.txt"),
                "old");
        }

        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(executableDirectory, "config"));
        CreateWriter(location).Write("Startup.Initialize", CreateException());

        string[] crashFiles = Directory.GetFiles(logsDirectory, "crash-*.txt");
        Assert.Equal(5, crashFiles.Length);
        Assert.DoesNotContain(
            crashFiles,
            path => Path.GetFileName(path) == "crash-20260101-000000.txt");
        Assert.Contains(
            crashFiles,
            path => Path.GetFileName(path) == "crash-20261025-143015.txt");
    }

    [Fact]
    public void Write_GivenMemoryOnlySettings_DoesNotThrowOrWriteAnything()
    {
        Directory.CreateDirectory(_root);
        CrashReportWriter writer = CreateWriter(SettingsLocation.MemoryOnly);

        writer.Write("Startup.Initialize", CreateException());

        Assert.Empty(Directory.GetFiles(_root, "crash-*.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public void Write_GivenTheLogDirectoryCannotBeCreated_DoesNotThrow()
    {
        Directory.CreateDirectory(_root);
        string blockedParent = Path.Combine(_root, "app");
        File.WriteAllText(blockedParent, "not a directory");
        SettingsLocation location = SettingsLocation.ForDirectory(
            SettingsLocationKind.ExecutableDirectory,
            Path.Combine(blockedParent, "config"));
        CrashReportWriter writer = CreateWriter(location);

        writer.Write("Startup.Initialize", CreateException());

        Assert.Empty(Directory.GetFiles(_root, "crash-*.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public void Write_GivenAProfilePathInTheException_RedactsIt()
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
        var exception = new InvalidOperationException(Path.Combine(userProfile, "secret.txt"));

        CreateWriter(location).Write("Startup.Initialize", exception);

        string crashFile = Assert.Single(
            Directory.GetFiles(Path.Combine(executableDirectory, "logs"), "crash-*.txt"));
        string content = File.ReadAllText(crashFile);
        Assert.Contains("%USERPROFILE%", content, StringComparison.Ordinal);
        Assert.DoesNotContain(userProfile, content, StringComparison.OrdinalIgnoreCase);
    }

    private static CrashReportWriter CreateWriter(SettingsLocation location) =>
        new(
            new StubSettingsStore(location),
            new AssemblyApplicationInfo(typeof(CrashReportWriter).Assembly),
            new FixedTimeProvider(_now));

    private static InvalidOperationException CreateException()
    {
        InvalidOperationException exception;
        try
        {
            throw new InvalidOperationException("The shell could not continue.");
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        return exception;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class StubSettingsStore : ISettingsStore
    {
        public StubSettingsStore(SettingsLocation location) => Location = location;

        public SettingsLocation Location
        {
            get;
        }

        public AppSettings Current { get; } = AppSettings.CreateDefault();

        public SettingsLoadResult Load() => new(Current, Location, SettingsLoadIssue.None);

        public SettingsSaveResult Save(AppSettings settings) => SettingsSaveResult.Success;
    }
}
