using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
using JiuMi.Errors;
using JiuMi.Infrastructure;
using JiuMi.ViewModels;
using JiuMi.Views;
using Microsoft.Extensions.DependencyInjection;

namespace JiuMi.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using ServiceProvider services = CreateServices();
        ReportStartupIssues(services);
        SubscribeProcessHandlers(services);

        IErrorWindowPresenter errorWindows = services.GetRequiredService<IErrorWindowPresenter>();
        return AppBuilder.Configure(
                () => new App(() => services.GetRequiredService<MainWindow>(), errorWindows))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<ILocalizationService>(provider =>
        {
            ISettingsStore settingsStore = provider.GetRequiredService<ISettingsStore>();
            LocalizationCatalog catalog = provider.GetRequiredService<ILocalizationSource>().Load();
            var localization = new LocalizationService(
                catalog,
                settingsStore.Current.Language ?? CultureInfo.CurrentUICulture.Name);

            if (!string.Equals(
                settingsStore.Current.Language,
                localization.CurrentLanguage,
                StringComparison.Ordinal))
            {
                // Persist the resolved concrete code so the next start does not repeat the resolution.
                _ = settingsStore.Save(settingsStore.Current with
                {
                    Language = localization.CurrentLanguage,
                });
            }

            return localization;
        });
        services.AddSingleton<InfoBarViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<ErrorWindowCoordinator>();
        services.AddSingleton<IErrorWindowPresenter>(provider => new ErrorWindowPresenter(
            provider.GetRequiredService<IErrorReporter>(),
            provider.GetRequiredService<ErrorRecordFactory>(),
            provider.GetRequiredService<ILocalizationService>(),
            provider.GetRequiredService<ErrorWindowCoordinator>(),
            IsMainWindowAvailable,
            RequestExit));
        return services.BuildServiceProvider();
    }

    private static void ReportStartupIssues(ServiceProvider services)
    {
        SettingsLoadResult result = services.GetRequiredService<ISettingsStore>().Load();
        IErrorReporter reporter = services.GetRequiredService<IErrorReporter>();
        ErrorRecordFactory recordFactory = services.GetRequiredService<ErrorRecordFactory>();

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

        // The UI is not safe to touch here: record what is known and let the runtime terminate.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Environment.ExitCode = 1;
            if (e.ExceptionObject is Exception error)
            {
                reporter.Report(recordFactory.Create(
                    ErrorCodes.UnexpectedAppDomainException,
                    "AppDomain.UnhandledException",
                    error));
            }

            if (!e.IsTerminating)
            {
                Environment.Exit(1);
            }
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

