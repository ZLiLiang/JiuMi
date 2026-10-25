using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using JiuMi.Application;
using JiuMi.Application.Diagnostics;
using JiuMi.Application.Errors;
using JiuMi.Application.Settings;
using JiuMi.Infrastructure.Logging;

namespace JiuMi.Infrastructure.Diagnostics;

/// <summary>
/// Writes the independent plain-text crash file that survives a process that cannot continue.
/// </summary>
public sealed class CrashReportWriter : ICrashReportWriter
{
    private const int RetainedFileCount = 5;
    private const string FilePattern = "crash-*.txt";

    private readonly ISettingsStore _settingsStore;
    private readonly IApplicationInfo _applicationInfo;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrashReportWriter"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies the effective location and config path.</param>
    /// <param name="applicationInfo">Supplies the application version.</param>
    /// <param name="timeProvider">Supplies the crash time and file name.</param>
    public CrashReportWriter(
        ISettingsStore settingsStore,
        IApplicationInfo applicationInfo,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(applicationInfo);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _settingsStore = settingsStore;
        _applicationInfo = applicationInfo;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The crash file is a last-resort diagnostic: failing to write it must not mask the original failure.")]
    public void Write(string operation, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            string? directory = LogPaths.For(_settingsStore.Location);
            if (directory is null)
            {
                return;
            }

            Directory.CreateDirectory(directory);
            DateTimeOffset timestamp = _timeProvider.GetLocalNow();
            string path = Path.Combine(
                directory,
                $"crash-{timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.txt");

            try
            {
                File.WriteAllText(path, Format(operation, exception, timestamp), new UTF8Encoding(false));
            }
            catch
            {
                TryDelete(path);
                throw;
            }

            Prune(directory);
        }
        catch (Exception)
        {
            // Both locations being unwritable is an accepted outcome: the details remain copyable.
        }
    }

    private string Format(string operation, Exception exception, DateTimeOffset timestamp)
    {
        var builder = new StringBuilder();
        AppendField(builder, "timestamp", timestamp.ToString("O", CultureInfo.InvariantCulture));
        AppendField(builder, "appVersion", _applicationInfo.Version);
        AppendField(builder, "operation", operation);
        AppendField(builder, "exceptionType", exception.GetType().FullName);
        AppendField(builder, "exceptionMessage", exception.Message);

        builder.Append("stackTrace:").AppendLine();
        if (!string.IsNullOrWhiteSpace(exception.StackTrace))
        {
            foreach (string line in exception.StackTrace.Split('\n'))
            {
                builder.Append("  ").AppendLine(ErrorRedactor.Redact(line.TrimEnd('\r')));
            }
        }

        AppendField(builder, "configPath", _settingsStore.Location.FilePath ?? "(memory only)");
        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string key, string? value)
    {
        builder
            .Append(key)
            .Append(": ")
            .AppendLine(ErrorRedactor.Redact(value ?? string.Empty));
    }

    private static void Prune(string directory)
    {
        string[] files = Directory.GetFiles(directory, FilePattern);
        if (files.Length <= RetainedFileCount)
        {
            return;
        }

        Array.Sort(files, StringComparer.Ordinal);
        foreach (string file in files.AsSpan(0, files.Length - RetainedFileCount).ToArray())
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
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
