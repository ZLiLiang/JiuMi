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
    public void ApplyStoredTheme_GivenStoredThemeFails_FallsBackToSystem()
    {
        var store = new StubSettingsStore(AppTheme.Dark);
        var applier = new StubThemeApplier();
        applier.Available.Add(AppTheme.System);
        var service = new ThemeService(store, applier);

        service.ApplyStoredTheme();

        Assert.Equal(AppTheme.System, service.Current);
        Assert.Equal(new[] { AppTheme.Dark, AppTheme.System }, applier.Applied);
    }

    [Fact]
    public void ApplyStoredTheme_GivenStoredThemeFails_KeepsTheStoredPreference()
    {
        var store = new StubSettingsStore(AppTheme.Dark);
        var applier = new StubThemeApplier();
        applier.Available.Add(AppTheme.System);
        var service = new ThemeService(store, applier);

        service.ApplyStoredTheme();

        // The failure may be temporary, so the next start must retry the user's choice.
        Assert.Equal(AppTheme.Dark, store.Current.Theme);
    }

    [Fact]
    public void TrySetTheme_GivenThemeCanBeApplied_AppliesIt()
    {
        var service = new ThemeService(new StubSettingsStore(AppTheme.System), CreateApplier());

        bool applied = service.TrySetTheme(AppTheme.Light);

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

        bool applied = service.TrySetTheme(AppTheme.Dark);

        Assert.False(applied);
        Assert.Equal(AppTheme.Light, service.Current);
        Assert.Equal(
            new[] { AppTheme.Light, AppTheme.Dark, AppTheme.Light },
            applier.Applied);
    }

    [Fact]
    public void TrySetTheme_GivenSwitchAndRestoreFail_FallsBackToSystem()
    {
        var applier = CreateApplier();
        var service = new ThemeService(new StubSettingsStore(AppTheme.Light), applier);
        service.ApplyStoredTheme();
        applier.Available.Remove(AppTheme.Light);
        applier.Available.Remove(AppTheme.Dark);

        bool applied = service.TrySetTheme(AppTheme.Dark);

        Assert.False(applied);
        Assert.Equal(AppTheme.System, service.Current);
    }

    [Fact]
    public void TrySetTheme_GivenCurrentTheme_DoesNotApplyItAgain()
    {
        var applier = CreateApplier();
        var service = new ThemeService(new StubSettingsStore(AppTheme.System), applier);

        bool applied = service.TrySetTheme(AppTheme.System);

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

        public bool TryApply(AppTheme theme)
        {
            Applied.Add(theme);
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
