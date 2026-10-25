using System.ComponentModel;
using System.Globalization;
using JiuMi.Application;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
using JiuMi.Threading;
using JiuMi.ViewModels;

namespace JiuMi.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void SelectThemeCommand_GivenSwitchSucceeds_SavesTheSelection()
    {
        var page = new PageHarness(storedTheme: AppTheme.System, appliedTheme: AppTheme.System);

        page.ViewModel.SelectThemeCommand.Execute(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, page.ThemeService.Current);
        Assert.Equal(AppTheme.Dark, page.Store.Current.Theme);
        Assert.Empty(page.Reporter.Reports);
    }

    [Fact]
    public void SelectThemeCommand_GivenSwitchFails_KeepsTheStoredPreferenceAndReports()
    {
        var page = new PageHarness(storedTheme: AppTheme.System, appliedTheme: AppTheme.System);
        var failure = new InvalidOperationException("Theme host unavailable.");
        page.ThemeService.Failure = failure;
        var changed = new List<string?>();
        page.ViewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        page.ViewModel.SelectThemeCommand.Execute(AppTheme.Dark);
        page.Dispatcher.RunPending();

        Assert.Equal(AppTheme.System, page.Store.Current.Theme);
        ErrorRecord report = Assert.Single(page.Reporter.Reports);
        Assert.Equal(ErrorCodes.ThemeSwitchFailed, report.Code);
        Assert.Equal(failure.Message, report.ExceptionMessage);
        Assert.Equal(
            [
                nameof(SettingsViewModel.IsFollowSystemTheme),
                nameof(SettingsViewModel.IsLightTheme),
                nameof(SettingsViewModel.IsDarkTheme),
            ],
            changed);
    }

    [Fact]
    public void SelectThemeCommand_GivenAppliedThemeDiffersFromStored_RecordsTheChoice()
    {
        // The stored theme failed at startup, so the applied preference is behind the file.
        var page = new PageHarness(storedTheme: AppTheme.Dark, appliedTheme: AppTheme.System);

        page.ViewModel.SelectThemeCommand.Execute(AppTheme.System);

        Assert.Equal(AppTheme.System, page.Store.Current.Theme);
        Assert.Empty(page.Reporter.Reports);
    }

    private sealed class PageHarness
    {
        public PageHarness(AppTheme storedTheme, AppTheme appliedTheme)
        {
            Store = new StubSettingsStore(storedTheme);
            ThemeService = new StubThemeService(appliedTheme);
            Dispatcher = new StubUiThreadDispatcher();
            Reporter = new StubErrorReporter();
            var applicationInfo = new StubApplicationInfo();
            ViewModel = new SettingsViewModel(
                applicationInfo,
                Reporter,
                new ErrorRecordFactory(applicationInfo, Store, new FixedTimeProvider()),
                new StubLocalizationService(),
                Store,
                ThemeService,
                Dispatcher,
                new StubSystemLauncher());
        }

        public SettingsViewModel ViewModel
        {
            get;
        }

        public StubSettingsStore Store
        {
            get;
        }

        public StubThemeService ThemeService
        {
            get;
        }

        public StubUiThreadDispatcher Dispatcher
        {
            get;
        }

        public StubErrorReporter Reporter
        {
            get;
        }
    }

    private sealed class StubThemeService : IThemeService
    {
        public StubThemeService(AppTheme current) => Current = current;

        public bool SwitchSucceeds { get; set; } = true;

        public Exception? Failure
        {
            get; set;
        }

        public AppTheme Current
        {
            get; private set;
        }

        public void ApplyStoredTheme()
        {
        }

        public bool TrySetTheme(AppTheme theme, out Exception? failure)
        {
            failure = null;
            if (Failure is not null)
            {
                failure = Failure;
                return false;
            }

            if (!SwitchSucceeds)
            {
                return false;
            }

            Current = theme;
            return true;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class StubSettingsStore : ISettingsStore
    {
        public StubSettingsStore(AppTheme theme) => Current = new AppSettings
        {
            Language = "zh-CN",
            Theme = theme,
        };

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

    private sealed class StubErrorReporter : IErrorReporter
    {
        public ErrorDetailBuffer Details
        {
            get;
        } = new();

        public List<ErrorRecord> Reports
        {
            get;
        } = [];

        public event EventHandler<ErrorRecord>? Reported;

        public void Report(ErrorRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            Reports.Add(record);
            Reported?.Invoke(this, Details.Add(record));
        }
    }

    private sealed class StubUiThreadDispatcher : IUiThreadDispatcher
    {
        private readonly List<Action> _pending = [];

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            _pending.Add(action);
        }

        public void RunPending()
        {
            Action[] pending = [.. _pending];
            _pending.Clear();
            foreach (Action action in pending)
            {
                action();
            }
        }
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        private static readonly LanguageOption[] _languages = [new("zh-CN", "中文")];

        public string this[string key] => key;

        public IReadOnlyList<LanguageOption> Languages => _languages;

        public string CurrentLanguage => "zh-CN";

        public CultureInfo Culture => CultureInfo.GetCultureInfo("zh-CN");

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public string Format(string key, params object?[] arguments) => key;

        public bool TrySetLanguage(string languageCode) => false;
    }

    private sealed class StubApplicationInfo : IApplicationInfo
    {
        public string Name => "JiuMi";

        public string Version => "1.0.0";

        public string RepositoryUrl => "https://github.com/ZLiLiang/JiuMi";
    }

    private sealed class StubSystemLauncher : ISystemLauncher
    {
        public void OpenFolder(string path)
        {
        }

        public void OpenUri(Uri uri)
        {
        }
    }
}
