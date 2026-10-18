using System.Reflection;
using JiuMi.Application;
using JiuMi.Application.Localization;
using JiuMi.Infrastructure.Localization;
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
        return services;
    }
}
