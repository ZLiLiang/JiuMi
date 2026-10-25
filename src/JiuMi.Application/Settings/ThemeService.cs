namespace JiuMi.Application.Settings;

/// <summary>
/// Keeps the theme preference in effect and falls back to the system theme when one cannot be applied.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly IThemeApplier _applier;
    private readonly ISettingsStore _settingsStore;
    private AppTheme _current = AppTheme.System;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeService"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies the stored preference at startup.</param>
    /// <param name="applier">Applies preferences to the running interface.</param>
    public ThemeService(ISettingsStore settingsStore, IThemeApplier applier)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(applier);
        _settingsStore = settingsStore;
        _applier = applier;
    }

    /// <inheritdoc />
    public AppTheme Current => _current;

    /// <inheritdoc />
    public void ApplyStoredTheme()
    {
        AppTheme stored = _settingsStore.Current.Theme;
        if (_applier.TryApply(stored, out _))
        {
            _current = stored;
            return;
        }

        // The file keeps the user's choice: the failure may be temporary, and the parser already
        // normalizes values that are not part of the preference model.
        FallBackToSystem();
    }

    /// <inheritdoc />
    public bool TrySetTheme(AppTheme theme, out Exception? failure)
    {
        failure = null;
        if (theme == _current)
        {
            return true;
        }

        if (_applier.TryApply(theme, out failure))
        {
            _current = theme;
            return true;
        }

        if (!_applier.TryApply(_current, out Exception? restoreFailure))
        {
            failure ??= restoreFailure;
            FallBackToSystem();
        }

        return false;
    }

    private void FallBackToSystem()
    {
        _current = AppTheme.System;
        _ = _applier.TryApply(_current, out _);
    }
}
