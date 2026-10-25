using JiuMi.Application.Settings;
using JiuMi.Infrastructure.Settings;

namespace JiuMi.Infrastructure.Tests;

public sealed class SettingsLocationProviderTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly string _executableDirectory;
    private readonly string _appDataDirectory;

    public SettingsLocationProviderTests()
    {
        _executableDirectory = Path.Combine(_root, "app");
        _appDataDirectory = Path.Combine(_root, "appdata", "JiuMi");
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
    public void Locate_GivenWritableExecutableDirectory_ReturnsExecutableLocation()
    {
        SettingsLocation location = CreateProvider().Locate();

        string configDirectory = Path.Combine(_executableDirectory, "config");
        Assert.Equal(SettingsLocationKind.ExecutableDirectory, location.Kind);
        Assert.Equal(configDirectory, location.DirectoryPath);
        Assert.Equal(Path.Combine(configDirectory, SettingsContract.FileName), location.FilePath);
        Assert.Empty(Directory.GetFiles(configDirectory, ".settings-probe-*"));
    }

    [Fact]
    public void Locate_GivenExecutableConfigIsAFile_ReturnsAppDataLocation()
    {
        File.WriteAllText(Path.Combine(_executableDirectory, "config"), "blocked");

        SettingsLocation location = CreateProvider().Locate();

        Assert.Equal(SettingsLocationKind.AppData, location.Kind);
        Assert.Equal(_appDataDirectory, location.DirectoryPath);
        Assert.True(Directory.Exists(_appDataDirectory));
    }

    [Fact]
    public void Locate_GivenExistingSettingsFileLocked_ReturnsAppDataLocation()
    {
        string configDirectory = Path.Combine(_executableDirectory, "config");
        Directory.CreateDirectory(configDirectory);
        string settingsPath = Path.Combine(configDirectory, SettingsContract.FileName);
        File.WriteAllText(settingsPath, "{}");

        using (new FileStream(settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            SettingsLocation location = CreateProvider().Locate();

            Assert.Equal(SettingsLocationKind.AppData, location.Kind);
        }
    }

    [Fact]
    public void Locate_GivenBothCandidatesBlocked_ReturnsMemoryOnly()
    {
        File.WriteAllText(Path.Combine(_executableDirectory, "config"), "blocked");
        Directory.CreateDirectory(Path.GetDirectoryName(_appDataDirectory)!);
        File.WriteAllText(_appDataDirectory, "blocked");

        SettingsLocation location = CreateProvider().Locate();

        Assert.Equal(SettingsLocationKind.MemoryOnly, location.Kind);
        Assert.Null(location.DirectoryPath);
        Assert.Null(location.FilePath);
        Assert.False(location.IsPersistent);
    }

    private SettingsLocationProvider CreateProvider() =>
        new(_executableDirectory, _appDataDirectory);
}
