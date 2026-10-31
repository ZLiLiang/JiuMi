using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using JiuMi.Application.Diagnostics;
using JiuMi.Application.Errors;
using JiuMi.Application.ImageConversion;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
using JiuMi.Application.Shell;
using JiuMi.Diagnostics;
using JiuMi.Errors;
using JiuMi.Infrastructure;
using JiuMi.Infrastructure.Logging;
using JiuMi.Shell;
using JiuMi.Startup;
using JiuMi.Theming;
using JiuMi.Threading;
using JiuMi.ViewModels;
using JiuMi.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace JiuMi.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using ServiceProvider services = CreateServices();
        Log.Logger = services.GetRequiredService<LoggingPipeline>().Logger;
        SubscribeProcessHandlers(services);

        // The lifecycle subscribes to the activation channel before the listener starts, so a launch
        // that arrives during startup is queued instead of being lost.
        ShellLifecycle lifecycle = services.GetRequiredService<ShellLifecycle>();

        // The instance lock and its pipe exist before the interface starts.
        ISingleInstanceCoordinator singleInstance =
            services.GetRequiredService<ISingleInstanceCoordinator>();
        if (!singleInstance.TryAcquire())
        {
            Log.CloseAndFlush();
            return ExitCodes.Success;
        }

        IErrorWindowPresenter errorWindows = services.GetRequiredService<IErrorWindowPresenter>();
        IShellStartup startup = new SplashStartup(
            CreateInitialization(services),
            services.GetRequiredService<IErrorReporter>(),
            services.GetRequiredService<ErrorRecordFactory>(),
            errorWindows,
            lifecycle,
            services.GetRequiredService<Serilog.ILogger>());

        int exitCode = AppBuilder.Configure(() => new App(startup, errorWindows))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);

        Log.CloseAndFlush();
        return exitCode;
    }

    /// <summary>
    /// Builds the startup work the splash covers, in the order the architecture fixes it.
    /// </summary>
    /// <param name="services">The composition root every step reads from.</param>
    /// <returns>The ordered steps, with the shell window construction after them.</returns>
    private static ShellInitialization CreateInitialization(ServiceProvider services)
    {
        IThemeService themeService = services.GetRequiredService<IThemeService>();

        // The probe has to precede the first settings read, the text catalogue has to exist before the
        // interface is built, and the theme is a UI resource that must switch on the interface thread.
        // The settings and localization contracts are synchronous file I/O; offloading them keeps the
        // splash drawing, which is what makes the readiness timeout observable. Revisit when those
        // contracts grow async APIs.
        return new ShellInitialization(
            [
                () => Task.Run(() => RunConfigProbe(services)),
                () => Task.Run(() => ReportStartupIssues(services)),
                () => RunLocalizationAsync(services),
                () =>
                {
                    themeService.ApplyStoredTheme();
                    return Task.CompletedTask;
                },
            ],
            () => services.GetRequiredService<MainWindow>());
    }

    private static void RunConfigProbe(ServiceProvider services)
    {
        // Settling the location is the probe: this session reads and writes wherever it lands. The
        // log file follows it, so the file sink can only be attached after this point.
        ISettingsStore settingsStore = services.GetRequiredService<ISettingsStore>();
        SettingsLocation location = settingsStore.Location;
        services.GetRequiredService<LoggingPipeline>().AttachFileLogging(location);
        services.GetRequiredService<Serilog.ILogger>().Information(
            "Settings location {Kind} at {ConfigPath}",
            location.Kind,
            location.FilePath ?? "memory only");
    }

    private static async Task RunLocalizationAsync(ServiceProvider services)
    {
        ISettingsStore settingsStore = services.GetRequiredService<ISettingsStore>();

        // Settings are read and the catalogue is loaded off the interface thread. The service itself
        // is built below, on the interface thread, because it applies the interface culture to the
        // calling thread.
        string? storedLanguage = await Task.Run(() =>
        {
            SettingsLoadResult result = settingsStore.Load();
            _ = services.GetRequiredService<LocalizationCatalog>();
            return result.Settings.Language;
        });

        ILocalizationService localization = services.GetRequiredService<ILocalizationService>();

        // The fallback may have built the shell before this step ran, which constructed the service
        // with the defaults. Applying the stored code here is what makes the late result land hot.
        if (storedLanguage is not null)
        {
            _ = localization.TrySetLanguage(storedLanguage);
        }

        if (!string.Equals(
            settingsStore.Current.Language,
            localization.CurrentLanguage,
            StringComparison.Ordinal))
        {
            // Persist the resolved concrete code so the next start does not repeat the resolution.
            await Task.Run(() => settingsStore.Save(settingsStore.Current with
            {
                Language = localization.CurrentLanguage,
            }));
        }
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<IThemeApplier>(new AvaloniaThemeApplier(
            static () => Avalonia.Application.Current));
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IUiThreadDispatcher, AvaloniaUiThreadDispatcher>();
        services.AddSingleton<LocalizationCatalog>(
            provider => provider.GetRequiredService<ILocalizationSource>().Load());
        services.AddSingleton<ILocalizationService>(provider =>
        {
            ISettingsStore settingsStore = provider.GetRequiredService<ISettingsStore>();
            LocalizationCatalog catalog = provider.GetRequiredService<LocalizationCatalog>();
            return new LocalizationService(
                catalog,
                settingsStore.Current.Language ?? CultureInfo.CurrentUICulture.Name,
                provider.GetRequiredService<Serilog.ILogger>());
        });
        services.AddSingleton<DiagnosticsExportController>();
        services.AddSingleton<InfoBarViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<WorldClockViewModel>();
        services.AddSingleton(new OutputPathPlanner(File.Exists));
        services.AddSingleton<ImageConvertViewModel>();
        services.AddSingleton<FileRenameViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ErrorWindowCoordinator>();
        services.AddSingleton<ShellLifecycle>();
        services.AddSingleton<IErrorWindowPresenter>(provider => new ErrorWindowPresenter(
            provider.GetRequiredService<IErrorReporter>(),
            provider.GetRequiredService<ErrorRecordFactory>(),
            () => provider.GetRequiredService<ILocalizationService>(),
            provider.GetRequiredService<ErrorWindowCoordinator>(),
            static () => new ErrorWindow(),
            IsMainWindowAvailable,
            RequestExit));
        return services.BuildServiceProvider();
    }

    private static void ReportStartupIssues(ServiceProvider services)
    {
        SettingsLoadResult result = services.GetRequiredService<ISettingsStore>().Load();
        IErrorReporter reporter = services.GetRequiredService<IErrorReporter>();
        ErrorRecordFactory recordFactory = services.GetRequiredService<ErrorRecordFactory>();
        Serilog.ILogger logger = services.GetRequiredService<Serilog.ILogger>();

        logger.Information(
            "Settings loaded from {Kind} with issues {Issues}",
            result.Location.Kind,
            result.Issues);

        if (result.Issues.HasFlag(SettingsLoadIssue.InvalidWorldClockPlaces))
        {
            logger.Warning("Settings contained world clock places that failed validation; they were dropped");
        }

        if (result.Issues.HasFlag(SettingsLoadIssue.Corrupted))
        {
            reporter.Report(recordFactory.Create(
                ErrorCodes.SettingsCorrupted,
                "Settings.Load",
                result.Error));
        }

        if (result.Issues.HasFlag(SettingsLoadIssue.FallbackToAppData))
        {
            reporter.Report(recordFactory.Create(
                ErrorCodes.ConfigFallbackToAppData,
                "Settings.Load"));
        }

        if (result.Issues.HasFlag(SettingsLoadIssue.MemoryOnly))
        {
            reporter.Report(recordFactory.Create(
                ErrorCodes.ConfigMemoryOnly,
                "Settings.Load"));
        }
    }

    private static void SubscribeProcessHandlers(ServiceProvider services)
    {
        IErrorReporter reporter = services.GetRequiredService<IErrorReporter>();
        ErrorRecordFactory recordFactory = services.GetRequiredService<ErrorRecordFactory>();
        ICrashReportWriter crashWriter = services.GetRequiredService<ICrashReportWriter>();
        Serilog.ILogger logger = services.GetRequiredService<Serilog.ILogger>();

        // These subscriptions live as long as the process, so they are deliberately never removed.
        // The UI is not safe to touch here: record what is known, flush, then leave with the failure code.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                crashWriter.Write("AppDomain.UnhandledException", exception);
                logger.Fatal(exception, "The process cannot continue");
            }
            else
            {
                logger.Fatal("The process cannot continue without an exception object");
            }

            Log.CloseAndFlush();
            Environment.Exit(ExitCodes.Failure);
        };

        // An abandoned task is not the user's problem: observe it and keep it out of the UI.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            reporter.Report(recordFactory.Create(
                ErrorCodes.UnexpectedUnobservedTask,
                "TaskScheduler.UnobservedTaskException",
                e.Exception,
                ErrorLevel.Warning));
        };
    }

    private static bool IsMainWindowAvailable() =>
        Avalonia.Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime { MainWindow: { IsLoaded: true } };

    private static void RequestExit(int exitCode)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(exitCode);
            return;
        }

        Environment.Exit(exitCode);
    }
}

