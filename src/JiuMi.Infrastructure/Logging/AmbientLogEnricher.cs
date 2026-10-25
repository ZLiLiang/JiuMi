using System.Runtime.InteropServices;
using Serilog.Core;
using Serilog.Events;

namespace JiuMi.Infrastructure.Logging;

/// <summary>
/// Adds the environment fields every log event carries.
/// </summary>
internal sealed class AmbientLogEnricher : ILogEventEnricher
{
    private static readonly string _osDescription = RuntimeInformation.OSDescription;

    private readonly string _appVersion;

    public AmbientLogEnricher(string appVersion)
    {
        _appVersion = appVersion;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("appVersion", _appVersion));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("osDescription", _osDescription));
    }
}
