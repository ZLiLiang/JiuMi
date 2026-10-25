using JiuMi.Application.Settings;

namespace JiuMi.Application.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public void ApplyStoredTheme_GivenStoredThemeCanBeApplied_AppliesIt()
    {
        var store = new StubSettingsStore(AppTheme.Dark);
        var applier = new StubThemeApplier();
        applier.Available.UnionWith([AppTheme.System, AppTheme.Dark]);
        var service = new ThemeService(store, applier);

        service.ApplyStoredTheme();

        Assert.Equal(AppTheme.Dark, service.Current);
        Assert.Equal(new[] { AppTheme.Dark }, applier.Applied);
    }

    [Fact]
    public void ApplyStoredTheme_GivenStoredThemeFails_FallsBackToTheLightPalette()
    {
        var store = new StubSettingsStore(AppTheme.Dark);
        var applier = new StubThemeApplier();
        applier.Available.Add(AppTheme.Light);
        var service = new ThemeService(store, applier);

        service.ApplyStoredTheme();

        // The light palette is Fluent's default; "Default" would resolve against the operating system.
        Assert.Equal(AppTheme.Light, service.Current);
        Assert.Equal(new[] { AppTheme.Dark, AppTheme.Light }, applier.Applied);
    }

    [Fact]
    public void ApplyStoredTheme_GivenStoredThemeFails_KeepsTheStoredPreference()
    {
        var store = new StubSettingsStore(AppTheme.Dark);
        var applier = new StubThemeApplier();
        applier.Available.Add(AppTheme.Light);
        var service = new ThemeService(store, applier);

        service.ApplyStoredTheme();

        // The failure may be temporary, so the next start must retry the user's choice.
        Assert.Equal(AppTheme.Dark, store.Current.Theme);
    }

    [Fact]
    public void ApplyStoredTheme_GivenStoredSystem_AppliesItAndKeepsTheStoredValue()
    {
        var store = new StubSettingsStore(AppTheme.System);
        var applier = CreateApplier();
        var service = new ThemeService(store, applier);

        service.ApplyStoredTheme();

        Assert.Equal(new[] { AppTheme.System }, applier.Applied);
        Assert.Equal(AppTheme.System, store.Current.Theme);
    }

    [Fact]
    public void TrySetTheme_GivenThemeCanBeApplied_AppliesIt()
    {
        var service = new ThemeService(new StubSettingsStore(AppTheme.System), CreateApplier());

        bool applied = service.TrySetTheme(AppTheme.Light, out _);

        Assert.True(applied);
        Assert.Equal(AppTheme.Light, service.Current);
    }

    [Fact]
    public void TrySetTheme_GivenSwitchFails_RestoresThePreviousTheme()
    {
        var applier = CreateApplier();
        var service = new ThemeService(new StubSettingsStore(AppTheme.Light), applier);
        service.ApplyStoredTheme();
        applier.Available.Remove(AppTheme.Dark);

        bool applied = service.TrySetTheme(AppTheme.Dark, out _);

        Assert.False(applied);
        Assert.Equal(AppTheme.Light, service.Current);
        Assert.Equal(
            new[] { AppTheme.Light, AppTheme.Dark, AppTheme.Light },
            applier.Applied);
    }

    [Fact]
    public void TrySetTheme_GivenThemeHostThrows_ReturnsTheFailureAndRestores()
    {
        var applier = CreateApplier();
        var service = new ThemeService(new StubSettingsStore(AppTheme.Light), applier);
        service.ApplyStoredTheme();
        var expectedFailure = new InvalidOperationException("Theme host unavailable.");
        applier.Failure = expectedFailure;

        bool applied = service.TrySetTheme(AppTheme.Dark, out Exception? failure);

        Assert.False(applied);
        Assert.Same(expectedFailure, failure);
        Assert.Equal(AppTheme.Light, service.Current);
    }

    [Fact]
    public void TrySetTheme_GivenSwitchAndRestoreFail_KeepsTheLastAppliedTheme()
    {
        var applier = CreateApplier();
        var service = new ThemeService(new StubSettingsStore(AppTheme.System), applier);
        service.ApplyStoredTheme();
        applier.Available.Remove(AppTheme.System);
        applier.Available.Remove(AppTheme.Dark);

        bool applied = service.TrySetTheme(AppTheme.Dark, out _);

        Assert.False(applied);
        // A failed switch must not pick a third theme; the preference on screen stays current.
        Assert.Equal(AppTheme.System, service.Current);
        Assert.Equal(AppTheme.System, applier.Applied[^1]);
    }

    [Fact]
    public void TrySetTheme_GivenEveryApplyFails_KeepsTheLastAppliedTheme()
    {
        var applier = CreateApplier();
        var service = new ThemeService(new StubSettingsStore(AppTheme.Dark), applier);
        service.ApplyStoredTheme();
        applier.Available.Remove(AppTheme.Light);
        applier.Available.Remove(AppTheme.Dark);

        bool applied = service.TrySetTheme(AppTheme.Light, out _);

        Assert.False(applied);
        // Nothing changed, so the preference reported as current must stay the one that is on screen.
        Assert.Equal(AppTheme.Dark, service.Current);
    }

    [Fact]
    public void TrySetTheme_GivenCurrentTheme_DoesNotApplyItAgain()
    {
        var applier = CreateApplier();
        var service = new ThemeService(new StubSettingsStore(AppTheme.System), applier);

        bool applied = service.TrySetTheme(AppTheme.System, out _);

        Assert.True(applied);
        Assert.Empty(applier.Applied);
    }

    private static StubThemeApplier CreateApplier()
    {
        var applier = new StubThemeApplier();
        applier.Available.UnionWith([AppTheme.System, AppTheme.Light, AppTheme.Dark]);
        return applier;
    }

    private sealed class StubThemeApplier : IThemeApplier
    {
        public HashSet<AppTheme> Available { get; } = [];

        public List<AppTheme> Applied { get; } = [];

        public Exception? Failure
        {
            get; set;
        }

        public bool TryApply(AppTheme theme, out Exception? failure)
        {
            failure = null;
            Applied.Add(theme);
            if (Failure is not null)
            {
                failure = Failure;
                Failure = null;
                return false;
            }

            return Available.Contains(theme);
        }
    }

    private sealed class StubSettingsStore : ISettingsStore
    {
        public StubSettingsStore(AppTheme theme)
        {
            Current = new AppSettings
            {
                Theme = theme,
            };
        }

        public SettingsLocation Location => SettingsLocation.MemoryOnly;

        public AppSettings Current
        {
            get; private set;
        }

        public SettingsLoadResult Load() => throw new NotSupportedException();

        public SettingsSaveResult Save(AppSettings settings)
        {
            Current = settings;
            return SettingsSaveResult.Success;
        }
    }
}
