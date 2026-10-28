using System.Text.Json;
using JiuMi.Application.Settings;
using JiuMi.Application.WorldClock;
using JiuMi.Infrastructure.Settings;

namespace JiuMi.Infrastructure.Tests;

public sealed class WorldClockSettingsStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly string _executableDirectory;
    private readonly string _settingsDirectory;
    private readonly string _settingsPath;

    public WorldClockSettingsStoreTests()
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
    public void Save_GivenWorldClock_WritesContractAndRoundTrips()
    {
        JsonSettingsStore store = CreateStore();
        store.Load();

        SettingsSaveResult result = store.Save(new AppSettings
        {
            OnlineDataEnabled = false,
            WorldClock = new WorldClockSettings(WorldClockViewMode.Plan,
            [
                new Place(
                    PlaceKind.Country,
                    "US",
                    new WorkWindow(new TimeOnly(9, 0), new TimeOnly(18, 0), [1, 2, 3, 4, 5])),
                new Place(
                    PlaceKind.City,
                    "Asia/Shanghai",
                    new WorkWindow(new TimeOnly(8, 30), new TimeOnly(17, 30), [1, 2, 3, 4, 5, 6])),
            ]),
        });

        Assert.True(result.Persisted);
        string json = File.ReadAllText(_settingsPath);
        Assert.Contains("\"onlineDataEnabled\": false", json);
        Assert.Contains("\"view\": \"plan\"", json);
        Assert.Contains("\"kind\": \"country\"", json);
        Assert.Contains("\"id\": \"US\"", json);
        Assert.Contains("\"start\": \"08:30\"", json);

        SettingsLoadResult reloaded = CreateStore().Load();
        Assert.Equal(SettingsLoadIssue.None, reloaded.Issues);
        Assert.False(reloaded.Settings.OnlineDataEnabled);
        Assert.Equal(WorldClockViewMode.Plan, reloaded.Settings.WorldClock.View);
        Assert.Equal(2, reloaded.Settings.WorldClock.Places.Count);
        Assert.Equal(PlaceKind.Country, reloaded.Settings.WorldClock.Places[0].Kind);
        Assert.Equal("US", reloaded.Settings.WorldClock.Places[0].Id);
        Assert.Equal(new TimeOnly(8, 30), reloaded.Settings.WorldClock.Places[1].Hours.Start);
        Assert.Equal(6, reloaded.Settings.WorldClock.Places[1].Hours.Days.Count);
    }

    [Fact]
    public void Load_GivenInvalidPlaces_DropsThemAndFlagsTheIssue()
    {
        WriteSettings("""
            {
              "onlineDataEnabled": false,
              "worldClock": {
                "view": "plan",
                "places": [
                  { "kind": "city", "id": "Asia/Shanghai", "start": "09:00", "end": "18:00", "days": [1, 2, 3, 4, 5] },
                  { "kind": "planet", "id": "Mars", "start": "09:00", "end": "18:00", "days": [1] },
                  { "kind": "city", "id": "Europe/London", "start": "18:00", "end": "09:00", "days": [1] },
                  { "kind": "city", "id": "Asia/Tokyo", "start": "09:00", "end": "18:00", "days": [] },
                  { "kind": "city", "id": "Asia/Shanghai", "start": "10:00", "end": "19:00", "days": [1] }
                ]
              }
            }
            """);

        SettingsLoadResult result = CreateStore().Load();

        Assert.True(result.Issues.HasFlag(SettingsLoadIssue.InvalidWorldClockPlaces));
        Assert.False(result.Settings.OnlineDataEnabled);
        Assert.Equal(WorldClockViewMode.Plan, result.Settings.WorldClock.View);
        Place place = Assert.Single(result.Settings.WorldClock.Places);
        Assert.Equal("Asia/Shanghai", place.Id);
        Assert.Equal(new TimeOnly(9, 0), place.Hours.Start);
    }

    [Fact]
    public void Load_GivenEmptyPlaces_KeepsTheEmptyList()
    {
        WriteSettings("""
            { "worldClock": { "view": "board", "places": [] } }
            """);

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(SettingsLoadIssue.None, result.Issues);
        Assert.Empty(result.Settings.WorldClock.Places);
    }

    [Fact]
    public void Load_GivenMissingWorldClock_UsesTheDefaults()
    {
        WriteSettings("""{ "language": "en-US" }""");

        SettingsLoadResult result = CreateStore().Load();

        Assert.Equal(SettingsLoadIssue.None, result.Issues);
        Assert.True(result.Settings.OnlineDataEnabled);
        Assert.Equal(WorldClockViewMode.Board, result.Settings.WorldClock.View);
        Assert.Equal(4, result.Settings.WorldClock.Places.Count);
    }

    [Fact]
    public void Load_GivenInvalidViewString_FallsBackToBoard()
    {
        WriteSettings("""
            { "worldClock": { "view": "boardwalk", "places": [] } }
            """);

        Assert.Equal(WorldClockViewMode.Board, CreateStore().Load().Settings.WorldClock.View);
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
