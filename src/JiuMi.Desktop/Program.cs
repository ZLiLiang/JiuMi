using System.Globalization;
using Avalonia;
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
        services.GetRequiredService<ISettingsStore>().Load();

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
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }
}

