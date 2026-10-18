using System.Reflection;
using JiuMi.Application;
using Microsoft.Extensions.DependencyInjection;

namespace JiuMi.Infrastructure.Tests;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_GivenServiceCollection_ResolvesEntryAssemblyMetadata()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();

        using ServiceProvider provider = services.BuildServiceProvider();

        IApplicationInfo info = provider.GetRequiredService<IApplicationInfo>();
        Assembly entryAssembly =
            Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("The test host has no entry assembly.");
        string expectedName =
            entryAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? entryAssembly.GetName().Name
            ?? string.Empty;
        string expectedVersion =
            AppVersion.FromInformationalVersion(
                entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

        Assert.Equal(expectedName, info.Name);
        Assert.Equal(expectedVersion, info.Version);
        Assert.Same(info, provider.GetRequiredService<IApplicationInfo>());
    }
}
