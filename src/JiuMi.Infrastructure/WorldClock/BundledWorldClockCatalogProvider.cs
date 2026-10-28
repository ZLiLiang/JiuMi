using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JiuMi.Application.WorldClock;

namespace JiuMi.Infrastructure.WorldClock;

/// <summary>
/// Reads the catalog files shipped with the application; the last rung of the fallback chain.
/// </summary>
internal sealed class BundledWorldClockCatalogProvider : IWorldClockCatalogProvider, IDisposable
{
    private readonly string _directory;
    private readonly Serilog.ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WorldClockCatalog? _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="BundledWorldClockCatalogProvider"/> class.
    /// </summary>
    /// <param name="directory">The directory holding the three catalog files.</param>
    /// <param name="logger">Receives read and validation problems.</param>
    public BundledWorldClockCatalogProvider(string directory, Serilog.ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(logger);
        _directory = directory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<WorldClockCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_catalog is { } loaded)
        {
            return loaded;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_catalog is { } raced)
            {
                return raced;
            }

            _catalog = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            return _catalog;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task<WorldClockCatalog> LoadCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            string countries = await File
                .ReadAllTextAsync(Path.Combine(_directory, "countries.json"), cancellationToken)
                .ConfigureAwait(false);
            string cities = await File
                .ReadAllTextAsync(Path.Combine(_directory, "cities.json"), cancellationToken)
                .ConfigureAwait(false);
            string officeHours = await File
                .ReadAllTextAsync(Path.Combine(_directory, "office-hours.json"), cancellationToken)
                .ConfigureAwait(false);

            if (!WorldClockCatalogJson.TryParse(
                    countries,
                    cities,
                    officeHours,
                    out WorldClockCatalog? catalog,
                    out IReadOnlyList<string> issues) ||
                catalog is null)
            {
                _logger.Warning("World clock catalog could not be parsed");
                return EmptyCatalog();
            }

            if (issues.Count > 0)
            {
                _logger.Warning(
                    "World clock catalog contains {IssueCount} data issues",
                    issues.Count);
                foreach (string issue in issues)
                {
                    _logger.Debug("World clock catalog issue: {Issue}", issue);
                }
            }

            return catalog;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(exception, "World clock catalog files could not be read");
            return EmptyCatalog();
        }
    }

    private static WorldClockCatalog EmptyCatalog() => new(
        [],
        [],
        new OfficeHoursCatalog(WorkWindow.Default, []));
}
