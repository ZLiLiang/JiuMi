using System.ComponentModel;

namespace JiuMi.Application.Settings;

/// <summary>
/// Keeps the theme preference in effect and falls back to Fluent's light palette when the stored
/// theme cannot be applied.
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
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public AppTheme Current => _current;

    /// <inheritdoc />
    public void ApplyStoredTheme()
    {
        AppTheme stored = _settingsStore.Current.Theme;
        if (_applier.TryApply(stored, out _))
        {
            SetCurrent(stored);
            return;
        }

        // The file keeps the user's choice: the failure may be temporary, and the parser already
        // normalizes values that are not part of the preference model. Architecture maps this path to
        // a Debug log entry; the log pipeline belongs to ADR-0004, so the host failure is converted
        // into the fallback instead of reaching the user.
        FallBackToLight();
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
            SetCurrent(theme);
            return true;
        }

        if (!_applier.TryApply(_current, out Exception? restoreFailure))
        {
            // An explicit switch never falls back to a third theme: the preference that was on screen
            // stays current, and the caller reports the rejection.
            failure ??= restoreFailure;
        }

        return false;
    }

    private void FallBackToLight()
    {
        // Fluent's fallback palette is the light one; "Default" resolves against the operating system,
        // which may hand back the very palette that just failed. A failed fallback leaves the last
        // applied preference in place, matching the applier's "nothing changed" contract.
        if (_applier.TryApply(AppTheme.Light, out _))
        {
            SetCurrent(AppTheme.Light);
        }
    }

    private void SetCurrent(AppTheme theme)
    {
        if (theme == _current)
        {
            return;
        }

        _current = theme;

        // Late startup work can change the theme after the shell is up; the settings page reads this
        // notification to keep its selection on the theme that is actually in effect.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
    }
}
