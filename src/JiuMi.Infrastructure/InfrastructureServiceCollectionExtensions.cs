using System.Reflection;
using JiuMi.Application;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
using JiuMi.Infrastructure.Errors;
using JiuMi.Infrastructure.Localization;
using JiuMi.Infrastructure.Settings;
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
        services.AddSingleton<ILocalizationSource>(
            _ => new LocalizationFileSource(Path.Combine(AppContext.BaseDirectory, "Assets", "i18n")));
        services.AddSingleton<ISettingsLocationProvider>(_ => new SettingsLocationProvider(
            AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JiuMi")));
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ISystemLauncher, ShellLauncher>();
        services.AddSingleton<IErrorReporter, ErrorReporter>();
        services.AddSingleton(provider => new ErrorRecordFactory(
            provider.GetRequiredService<IApplicationInfo>(),
            provider.GetRequiredService<ISettingsStore>(),
            TimeProvider.System));
        return services;
    }
}
