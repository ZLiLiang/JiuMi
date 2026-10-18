using System.Globalization;

namespace JiuMi.Application.Tests;

/// <summary>
/// Restores the process culture after a test changed it through the localization service.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _currentCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo _currentUiCulture = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CultureInfo.CurrentCulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
        _disposed = true;
    }
}
