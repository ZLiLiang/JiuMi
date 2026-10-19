using JiuMi.Application.Settings;

namespace JiuMi.Infrastructure.Settings;

/// <summary>
/// Probes the executable-side and per-user directories for a writable settings location.
/// </summary>
public sealed class SettingsLocationProvider : ISettingsLocationProvider
{
    private const string ConfigDirectoryName = "config";

    private readonly string _executableDirectory;
    private readonly string _appDataDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsLocationProvider"/> class.
    /// </summary>
    /// <param name="executableDirectory">The directory holding the application executable.</param>
    /// <param name="appDataDirectory">The per-user application data directory, <c>%AppData%\JiuMi\</c>.</param>
    public SettingsLocationProvider(string executableDirectory, string appDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);

        _executableDirectory = executableDirectory;
        _appDataDirectory = appDataDirectory;
    }

    /// <inheritdoc />
    public SettingsLocation Locate()
    {
        string configDirectory = Path.Combine(_executableDirectory, ConfigDirectoryName);
        if (CanUse(configDirectory))
        {
            return SettingsLocation.ForDirectory(SettingsLocationKind.ExecutableDirectory, configDirectory);
        }

        if (CanUse(_appDataDirectory))
        {
            return SettingsLocation.ForDirectory(SettingsLocationKind.AppData, _appDataDirectory);
        }

        return SettingsLocation.MemoryOnly;
    }

    private static bool CanUse(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            string probePath = Path.Combine(directory, ".settings-probe-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                }
            }
            finally
            {
                TryDelete(probePath);
            }

            string settingsPath = Path.Combine(directory, SettingsContract.FileName);
            if (File.Exists(settingsPath))
            {
                // A writable directory can still contain a read-only settings file, so probe it too.
                using (new FileStream(settingsPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                }
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
    }

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
