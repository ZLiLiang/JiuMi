using System.Reflection;
using JiuMi.Application;

namespace JiuMi.Infrastructure;

/// <summary>
/// Reads application metadata from the assembly attributes of a given assembly.
/// </summary>
public sealed class AssemblyApplicationInfo : IApplicationInfo
{
    private readonly Assembly _assembly;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyApplicationInfo"/> class.
    /// </summary>
    /// <param name="assembly">The assembly whose metadata is displayed.</param>
    public AssemblyApplicationInfo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _assembly = assembly;
    }

    /// <inheritdoc />
    public string Name =>
        _assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? _assembly.GetName().Name
        ?? string.Empty;

    /// <inheritdoc />
    public string Version =>
        AppVersion.FromInformationalVersion(
            _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
}
