using System.Globalization;
using Avalonia;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
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

        return AppBuilder.Configure(() => new App(() => services.GetRequiredService<MainWindow>()))
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
                ErrorLevel.Warning,
                ErrorCodes.SettingsCorrupted,
                "Settings.Load",
                result.Error));
        }

        if (result.Issues.HasFlag(SettingsLoadIssue.FallbackToAppData))
        {
            reporter.Report(recordFactory.Create(
                ErrorLevel.Warning,
                ErrorCodes.ConfigFallbackToAppData,
                "Settings.Load"));
        }

        if (result.Issues.HasFlag(SettingsLoadIssue.MemoryOnly))
        {
            reporter.Report(recordFactory.Create(
                ErrorLevel.Error,
                ErrorCodes.ConfigMemoryOnly,
                "Settings.Load"));
        }
    }
}

