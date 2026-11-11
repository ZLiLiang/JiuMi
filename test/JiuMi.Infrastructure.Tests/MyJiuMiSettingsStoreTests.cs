using System.Text.Json;
using JiuMi.Application.MyJiuMi;
using JiuMi.Application.Settings;
using JiuMi.Infrastructure.Settings;

namespace JiuMi.Infrastructure.Tests;

public sealed class MyJiuMiSettingsStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly string _executableDirectory;
    private readonly string _settingsDirectory;
    private readonly string _settingsPath;

    public MyJiuMiSettingsStoreTests()
    {
        _executableDirectory = Path.Combine(_root, "app");
        _settingsDirectory = Path.Combine(_executableDirectory, "config");
        _settingsPath = Path.Combine(_settingsDirectory, SettingsContract.FileName);
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
    public void Load_GivenNoFile_UsesTheCenteredDefault()
    {
        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(SettingsLoadIssue.None, result.Issues);
        Assert.Equal(MyJiuMiPositionRules.Default, result.Settings.MyJiuMi.X);
    }

    [Fact]
    public void Save_GivenPosition_WritesTheContractAndRoundTrips()
    {
        JsonSettingsStore store = CreateStore();
        store.Load();

        SettingsSaveResult result = store.Save(new AppSettings
        {
            MyJiuMi = new MyJiuMiSettings { X = 0.25 },
        });

        Assert.True(result.Persisted);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_settingsPath));
        Assert.Equal(0.25, document.RootElement
            .GetProperty("myJiuMi")
            .GetProperty("x")
            .GetDouble());

        MyJiuMiSettings reloaded = CreateStore().Load().Settings.MyJiuMi;
        Assert.Equal(0.25, reloaded.X);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.9999)]
    public void Load_GivenPositionAtAnEdge_KeepsIt(double x)
    {
        WriteSettings($$"""{ "myJiuMi": { "x": {{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } }""");

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(SettingsLoadIssue.None, result.Issues);
        Assert.Equal(x, result.Settings.MyJiuMi.X);
    }

    [Theory]
    [InlineData("""{ "myJiuMi": { "x": "left" } }""")]
    [InlineData("""{ "myJiuMi": { "x": true } }""")]
    [InlineData("""{ "myJiuMi": "not an object" }""")]
    public void Load_GivenNonNumericPosition_FallsBackToTheDefaultAndFlagsTheIssue(string json)
    {
        WriteSettings(json);

        SettingsLoadResult result = CreateStore().Load();

        Assert.True(result.Issues.HasFlag(SettingsLoadIssue.InvalidMyJiuMiPosition));
        Assert.Equal(MyJiuMiPositionRules.Default, result.Settings.MyJiuMi.X);
    }

    [Theory]
    [InlineData(-0.4)]
    [InlineData(1.4)]
    public void Load_GivenOutOfRangePosition_ClampsItAndFlagsTheIssue(double x)
    {
        WriteSettings($$"""{ "myJiuMi": { "x": {{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } }""");

        SettingsLoadResult result = CreateStore().Load();

        Assert.True(result.Issues.HasFlag(SettingsLoadIssue.InvalidMyJiuMiPosition));
        Assert.Equal(Math.Clamp(x, 0, 1), result.Settings.MyJiuMi.X);
    }

    [Fact]
    public void Load_GivenMyJiuMiWithoutX_UsesTheDefaultWithoutAnIssue()
    {
        WriteSettings("""{ "myJiuMi": { } }""");

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(SettingsLoadIssue.None, result.Issues);
        Assert.Equal(MyJiuMiPositionRules.Default, result.Settings.MyJiuMi.X);
    }

    [Fact]
    public void Load_GivenRetiredDesktopPetKeys_DropsThemOnRewrite()
    {
        WriteSettings("""
            {
              "petEnabled": true,
              "petX": 300,
              "petY": 200,
              "myJiuMi": { "x": 0.4 }
            }
            """);
        JsonSettingsStore store = CreateStore();
        store.Load();

        store.Save(store.Current);

        string json = File.ReadAllText(_settingsPath);
        Assert.DoesNotContain("petEnabled", json);
        Assert.DoesNotContain("petX", json);
        Assert.DoesNotContain("petY", json);
        Assert.Contains("\"myJiuMi\"", json);
    }

    private JsonSettingsStore CreateStore() =>
        new(new SettingsLocationProvider(
            _executableDirectory,
            Path.Combine(_root, "appdata", "JiuMi")));

    private void WriteSettings(string json)
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(_settingsPath, json);
    }
}
