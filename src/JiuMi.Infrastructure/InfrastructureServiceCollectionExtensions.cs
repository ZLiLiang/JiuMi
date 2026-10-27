using System.Reflection;
using System.Runtime.Versioning;
using JiuMi.Application;
using JiuMi.Application.Diagnostics;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Logging;
using JiuMi.Application.Notifications;
using JiuMi.Application.Settings;
using JiuMi.Application.Shell;
using JiuMi.Infrastructure.Diagnostics;
using JiuMi.Infrastructure.Errors;
using JiuMi.Infrastructure.Localization;
using JiuMi.Infrastructure.Logging;
using JiuMi.Infrastructure.Notifications;
using JiuMi.Infrastructure.Settings;
using JiuMi.Infrastructure.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace JiuMi.Infrastructure;

/// <summary>
/// Registers the infrastructure implementations of the application contracts.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the infrastructure services required by the application shell.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly entryAssembly =
            Assembly.GetEntryAssembly() ?? typeof(InfrastructureServiceCollectionExtensions).Assembly;
        services.AddSingleton<IApplicationInfo>(new AssemblyApplicationInfo(entryAssembly));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILocalizationSource>(
            _ => new LocalizationFileSource(Path.Combine(AppContext.BaseDirectory, "Assets", "i18n")));
        services.AddSingleton<ISettingsLocationProvider>(_ => new SettingsLocationProvider(
            AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JiuMi")));
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ISystemLauncher, ShellLauncher>();
        services.AddSingleton<ISingleInstanceCoordinator, SingleInstanceCoordinator>();

        if (OperatingSystem.IsWindows())
        {
            AddWindowsTray(services);
        }
        else
        {
            services.AddSingleton<ITrayIcon, NullTrayIcon>();
            services.AddSingleton<IUserNotification, NullUserNotification>();
        }

        services.AddSingleton(provider => new LoggingPipeline(
            provider.GetRequiredService<IApplicationInfo>(),
            Environment.GetEnvironmentVariable(LogLevelOverride.VariableName)));
        services.AddSingleton(provider => provider.GetRequiredService<LoggingPipeline>().Logger);
        services.AddSingleton(provider => provider.GetRequiredService<LoggingPipeline>().Status);

        services.AddSingleton<IErrorReporter>(provider => new ErrorReporter(
            provider.GetRequiredService<Serilog.ILogger>()));
        services.AddSingleton<ICrashReportWriter>(provider => new CrashReportWriter(
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<IApplicationInfo>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IDiagnosticsExporter>(provider => new DiagnosticsExporter(
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<IApplicationInfo>(),
            provider.GetRequiredService<ILoggingStatus>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new ErrorRecordFactory(
            provider.GetRequiredService<IApplicationInfo>(),
            provider.GetRequiredService<ISettingsStore>(),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsTray(IServiceCollection services)
    {
        // One window carries both the notification area entry and its balloon notifications.
        services.AddSingleton(provider => new WindowsTrayIcon(
            provider.GetRequiredService<Serilog.ILogger>()));
        services.AddSingleton<ITrayIcon>(provider => provider.GetRequiredService<WindowsTrayIcon>());
        services.AddSingleton<IUserNotification>(
            provider => provider.GetRequiredService<WindowsTrayIcon>());
    }
}
