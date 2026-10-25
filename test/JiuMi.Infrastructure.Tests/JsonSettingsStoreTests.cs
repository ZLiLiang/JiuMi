using System.Text.Json;
using JiuMi.Application.Settings;
using JiuMi.Infrastructure.Settings;

namespace JiuMi.Infrastructure.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private const string ValidSettings = """
        {
          "schemaVersion": 1,
          "language": "en-US",
          "theme": "light",
          "navExpanded": false
        }
        """;

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly string _executableDirectory;
    private readonly string _appDataDirectory;
    private readonly string _settingsDirectory;
    private readonly string _settingsPath;
    private readonly string _tempPath;
    private readonly string _backupPath;

    public JsonSettingsStoreTests()
    {
        _executableDirectory = Path.Combine(_root, "app");
        _appDataDirectory = Path.Combine(_root, "appdata", "JiuMi");
        _settingsDirectory = Path.Combine(_executableDirectory, "config");
        _settingsPath = Path.Combine(_settingsDirectory, SettingsContract.FileName);
        _tempPath = Path.Combine(_settingsDirectory, SettingsContract.TempFileName);
        _backupPath = Path.Combine(_settingsDirectory, SettingsContract.BackupFileName);
        Directory.CreateDirectory(_executableDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_GivenNoFile_ReturnsDefaultsWithoutWriting()
    {
        JsonSettingsStore store = CreateStore();

        SettingsLoadResult result = store.Load();

        Assert.Equal(SettingsLocationKind.ExecutableDirectory, result.Location.Kind);
        Assert.Equal(SettingsLoadIssue.None, result.Issues);
        Assert.Equal(AppSettings.CreateDefault(), result.Settings);
        Assert.Equal(result.Settings, store.Current);
        Assert.Null(result.Settings.Language);
        Assert.Equal(AppTheme.System, result.Settings.Theme);
        Assert.True(result.Settings.NavExpanded);
        Assert.Equal(SettingsContract.SchemaVersion, result.Settings.SchemaVersion);
        Assert.False(File.Exists(_settingsPath));
    }

    [Fact]
    public void Save_GivenSettings_WritesContractAndRoundTrips()
    {
        JsonSettingsStore store = CreateStore();
        store.Load();

        SettingsSaveResult result = store.Save(new AppSettings
        {
            Language = "en-US",
            Theme = AppTheme.Dark,
            NavExpanded = false,
        });

        Assert.True(result.Persisted);
        Assert.Null(result.Error);
        string json = File.ReadAllText(_settingsPath);
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"language\": \"en-US\"", json);
        Assert.Contains("\"theme\": \"dark\"", json);
        Assert.Contains("\"navExpanded\": false", json);
        Assert.False(File.Exists(_tempPath));

        SettingsLoadResult reloaded = CreateStore().Load();
        Assert.Equal("en-US", reloaded.Settings.Language);
        Assert.Equal(AppTheme.Dark, reloaded.Settings.Theme);
        Assert.False(reloaded.Settings.NavExpanded);
        Assert.Equal(SettingsLoadIssue.None, reloaded.Issues);
    }

    [Theory]
    [InlineData(AppTheme.System, "system")]
    [InlineData(AppTheme.Light, "light")]
    [InlineData(AppTheme.Dark, "dark")]
    public void Save_GivenTheme_WritesStorageValueAndRoundTrips(AppTheme theme, string storageValue)
    {
        JsonSettingsStore store = CreateStore();
        store.Load();

        SettingsSaveResult result = store.Save(new AppSettings { Theme = theme });

        Assert.True(result.Persisted);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_settingsPath));
        Assert.Equal(storageValue, document.RootElement.GetProperty("theme").GetString());
        Assert.Equal(theme, CreateStore().Load().Settings.Theme);
    }

    [Theory]
    [InlineData("system", AppTheme.System)]
    [InlineData("light", AppTheme.Light)]
    [InlineData("dark", AppTheme.Dark)]
    [InlineData("LIGHT", AppTheme.Light)]
    [InlineData("sepia", AppTheme.System)]
    [InlineData("", AppTheme.System)]
    public void Load_GivenThemeString_UsesKnownValueOrFallsBackToSystem(
        string storedValue,
        AppTheme expected)
    {
        WriteSettings($$"""{ "theme": "{{storedValue}}" }""");

        Assert.Equal(expected, CreateStore().Load().Settings.Theme);
    }

    [Fact]
    public void Load_GivenNonStringTheme_FallsBackToSystem()
    {
        WriteSettings("""{ "theme": 2 }""");

        Assert.Equal(AppTheme.System, CreateStore().Load().Settings.Theme);
    }

    [Fact]
    public void Load_GivenMissingKeys_UsesDefaults()
    {
        WriteSettings("""{ "language": "en-US" }""");

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(SettingsLoadIssue.None, result.Issues);
        Assert.Equal("en-US", result.Settings.Language);
        Assert.Equal(AppTheme.System, result.Settings.Theme);
        Assert.True(result.Settings.NavExpanded);
        Assert.Equal(SettingsContract.SchemaVersion, result.Settings.SchemaVersion);
    }

    [Fact]
    public void Load_GivenUnknownKeys_DropsThemOnRewrite()
    {
        WriteSettings("""
            {
              "schemaVersion": 1,
              "language": "en-US",
              "theme": "light",
              "navExpanded": false,
              "custom": "value"
            }
            """);
        JsonSettingsStore store = CreateStore();
        store.Load();

        store.Save(store.Current);

        string json = File.ReadAllText(_settingsPath);
        Assert.DoesNotContain("custom", json);
        Assert.Contains("\"theme\": \"light\"", json);
    }

    [Fact]
    public void Load_GivenCorruptJson_BacksUpFileAndReturnsDefaults()
    {
        const string corrupt = """{ "language": """;
        WriteSettings(corrupt);

        SettingsLoadResult result = CreateStore().Load();

        Assert.True(result.Issues.HasFlag(SettingsLoadIssue.Corrupted));
        Assert.NotNull(result.Error);
        Assert.Equal(AppSettings.CreateDefault(), result.Settings);
        Assert.False(File.Exists(_settingsPath));
        Assert.Equal(corrupt, File.ReadAllText(_backupPath));
    }

    [Fact]
    public void Load_GivenSecondCorruption_KeepsSingleBackup()
    {
        WriteSettings("not json");
        CreateStore().Load();

        const string secondCorrupt = """{ "schemaVersion": """;
        WriteSettings(secondCorrupt);
        CreateStore().Load();

        Assert.Single(Directory.GetFiles(_settingsDirectory, "*.bak"));
        Assert.Equal(secondCorrupt, File.ReadAllText(_backupPath));
    }

    [Fact]
    public void Load_GivenLeftoverTempFile_DeletesIt()
    {
        WriteSettings(ValidSettings);
        File.WriteAllText(_tempPath, "left over from a crash");

        SettingsLoadResult result = CreateStore().Load();

        Assert.False(File.Exists(_tempPath));
        Assert.Equal("en-US", result.Settings.Language);
    }

    [Fact]
    public void Load_GivenExecutableSideBlocked_ReportsFallbackToAppData()
    {
        File.WriteAllText(Path.Combine(_executableDirectory, "config"), "blocked");

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(SettingsLocationKind.AppData, result.Location.Kind);
        Assert.Equal(SettingsLoadIssue.FallbackToAppData, result.Issues);
        Assert.Equal(AppSettings.CreateDefault(), result.Settings);
    }

    [Fact]
    public void Load_GivenBothLocationsBlocked_RunsInMemoryOnly()
    {
        BlockBothLocations();

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(SettingsLocationKind.MemoryOnly, result.Location.Kind);
        Assert.Equal(SettingsLoadIssue.MemoryOnly, result.Issues);
        Assert.Equal(AppSettings.CreateDefault(), result.Settings);
    }

    [Fact]
    public void Save_GivenMemoryOnlyLocation_DoesNotPersist()
    {
        BlockBothLocations();
        JsonSettingsStore store = CreateStore();
        store.Load();

        SettingsSaveResult result = store.Save(new AppSettings { Language = "en-US" });

        Assert.False(result.Persisted);
        Assert.Null(result.Error);
        Assert.Equal("en-US", store.Current.Language);
    }

    [Fact]
    public void Save_GivenLockedSettingsFile_ReturnsFailureAndKeepsFile()
    {
        JsonSettingsStore store = CreateStore();
        store.Load();
        store.Save(new AppSettings { Language = "zh-CN" });
        string original = File.ReadAllText(_settingsPath);

        using (new FileStream(_settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            SettingsSaveResult result = store.Save(new AppSettings { Language = "en-US" });

            Assert.False(result.Persisted);
            Assert.True(result.Error is IOException or UnauthorizedAccessException);
        }

        Assert.Equal(original, File.ReadAllText(_settingsPath));
        Assert.False(File.Exists(_tempPath));
    }

    private JsonSettingsStore CreateStore() =>
        new(new SettingsLocationProvider(_executableDirectory, _appDataDirectory));

    private void WriteSettings(string json)
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(_settingsPath, json);
    }

    private void BlockBothLocations()
    {
        File.WriteAllText(Path.Combine(_executableDirectory, "config"), "blocked");
        Directory.CreateDirectory(Path.GetDirectoryName(_appDataDirectory)!);
        File.WriteAllText(_appDataDirectory, "blocked");
    }
}
