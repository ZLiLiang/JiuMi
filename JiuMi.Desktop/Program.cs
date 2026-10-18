using Avalonia;
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

        return AppBuilder.Configure(() => new App(() => services.GetRequiredService<MainWindow>()))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }
}

