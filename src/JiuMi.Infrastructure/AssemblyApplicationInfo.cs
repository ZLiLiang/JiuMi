using System.Reflection;
using JiuMi.Application;
using JiuMi.Application.Updates;

namespace JiuMi.Infrastructure;

/// <summary>
/// Reads application metadata from the assembly attributes of a given assembly.
/// </summary>
public sealed class AssemblyApplicationInfo : IApplicationInfo
{
    private const string DefaultRepositoryUrl = "https://github.com/ZLiLiang/JiuMi";
    private const string DefaultGiteeRepositoryUrl = "https://gitee.com/love-you-every-day/jiu-mi";
    private const string UpdateManifestPath = "/releases/download/app-update/update.json";

    private static readonly IReadOnlyList<AppUpdateSource> _defaultUpdateSources =
    [
        new AppUpdateSource(
            "Gitee",
            DefaultGiteeRepositoryUrl,
            new Uri(DefaultGiteeRepositoryUrl + UpdateManifestPath)),
        new AppUpdateSource(
            "GitHub",
            DefaultRepositoryUrl,
            new Uri(DefaultRepositoryUrl + UpdateManifestPath)),
    ];

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

    /// <inheritdoc />
    public string RepositoryUrl => DefaultRepositoryUrl;

    /// <inheritdoc />
    public string GiteeRepositoryUrl => DefaultGiteeRepositoryUrl;

    /// <inheritdoc />
    public IReadOnlyList<AppUpdateSource> UpdateSources => _defaultUpdateSources;
}
