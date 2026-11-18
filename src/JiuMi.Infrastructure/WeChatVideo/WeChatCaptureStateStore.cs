using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JiuMi.Application.Settings;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Persists the capture ports and proxy snapshot under <c>cache\wechat\state.json</c>.
/// </summary>
internal sealed class WeChatCaptureStateStore
{
    private static readonly string _relativePath = Path.Combine("cache", "wechat", "state.json");

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ISettingsStore? _settingsStore;
    private readonly string? _cacheDirectory;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatCaptureStateStore"/> class that follows the settings location.
    /// </summary>
    /// <param name="settingsStore">Supplies the effective config directory.</param>
    /// <param name="logger">Records write failures that the caller has already handled.</param>
    public WeChatCaptureStateStore(ISettingsStore settingsStore, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(logger);
        _settingsStore = settingsStore;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatCaptureStateStore"/> class for a fixed directory.
    /// </summary>
    /// <param name="cacheDirectory">The config directory, or <see langword="null"/> when settings are memory-only.</param>
    /// <param name="logger">Records write failures that the caller has already handled.</param>
    internal WeChatCaptureStateStore(string? cacheDirectory, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _cacheDirectory = cacheDirectory;
        _logger = logger;
    }

    private string? StatePath
    {
        get
        {
            string? directory = _settingsStore?.Location.DirectoryPath ?? _cacheDirectory;
            return directory is null ? null : Path.Combine(directory, _relativePath);
        }
    }

    /// <summary>
    /// Reads the state file, distinguishing a missing file from one that cannot be used.
    /// </summary>
    /// <returns>The read status and, when valid, the stored state.</returns>
    public WeChatCaptureStateReadResult Read()
    {
        string? path = StatePath;
        if (path is null || !File.Exists(path))
        {
            return new WeChatCaptureStateReadResult(WeChatCaptureStateReadStatus.Missing, null);
        }

        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!TryRead(document.RootElement, out WeChatCaptureState? state))
            {
                return new WeChatCaptureStateReadResult(WeChatCaptureStateReadStatus.Invalid, null);
            }

            return new WeChatCaptureStateReadResult(WeChatCaptureStateReadStatus.Valid, state);
        }
        catch (Exception exception) when (IsFileFailure(exception) || exception is JsonException)
        {
            _logger.Warning(exception, "WeChat capture state could not be read");
            return new WeChatCaptureStateReadResult(WeChatCaptureStateReadStatus.Invalid, null);
        }
    }

    /// <summary>
    /// Writes the state before the system proxy is switched.
    /// </summary>
    /// <param name="state">The ports and snapshot to persist.</param>
    /// <returns><see langword="false"/> when there is no writable location or the write failed.</returns>
    public bool Write(WeChatCaptureState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string? path = StatePath;
        if (path is null)
        {
            _logger.Warning("WeChat capture state cannot be persisted because settings are memory-only");
            return false;
        }

        string? directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            return false;
        }

        string tempPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            string json = JsonSerializer.Serialize(ToDocument(state), _serializerOptions);
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Warning(exception, "WeChat capture state could not be written");
            TryDelete(tempPath);
            return false;
        }
    }

    /// <summary>
    /// Removes the state file after a successful restore.
    /// </summary>
    /// <returns><see langword="false"/> when the file existed but could not be removed.</returns>
    public bool Delete()
    {
        string? path = StatePath;
        if (path is null)
        {
            return true;
        }

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            _logger.Warning(exception, "WeChat capture state could not be deleted");
            return false;
        }
    }

    private static bool TryRead(JsonElement root, out WeChatCaptureState? state)
    {
        state = null;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryReadPort(root, "proxyPort", out int proxyPort) ||
            !TryReadPort(root, "listenerPort", out int listenerPort) ||
            !root.TryGetProperty("startedAt", out JsonElement startedElement) ||
            startedElement.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParseExact(
                startedElement.GetString(),
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTimeOffset startedAt) ||
            !root.TryGetProperty("proxy", out JsonElement proxyElement) ||
            proxyElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        state = new WeChatCaptureState(
            proxyPort,
            listenerPort,
            startedAt,
            new WinInetProxySnapshot(
                ReadInt32(proxyElement, "proxyEnable"),
                ReadString(proxyElement, "proxyServer"),
                ReadString(proxyElement, "proxyOverride"),
                ReadString(proxyElement, "autoConfigUrl"),
                ReadInt32(proxyElement, "autoDetect")));
        return true;
    }

    private static bool TryReadPort(JsonElement root, string name, out int port)
    {
        port = 0;
        if (!root.TryGetProperty(name, out JsonElement element) ||
            element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt32(out port))
        {
            return false;
        }

        return port is >= 1 and <= 65535;
    }

    private static int? ReadInt32(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out int value)
            ? value
            : null;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement element) &&
            element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static WeChatCaptureStateDocument ToDocument(WeChatCaptureState state)
    {
        return new WeChatCaptureStateDocument
        {
            ProxyPort = state.ProxyPort,
            ListenerPort = state.ListenerPort,
            StartedAt = state.StartedAt.ToString("O", CultureInfo.InvariantCulture),
            Proxy = new WinInetProxySnapshotDocument
            {
                ProxyEnable = state.Proxy.ProxyEnable,
                ProxyServer = state.Proxy.ProxyServer,
                ProxyOverride = state.Proxy.ProxyOverride,
                AutoConfigUrl = state.Proxy.AutoConfigUrl,
                AutoDetect = state.Proxy.AutoDetect,
            },
        };
    }

    private static bool IsFileFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or NotSupportedException;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class WeChatCaptureStateDocument
    {
        public int ProxyPort
        {
            get;
            init;
        }

        public int ListenerPort
        {
            get;
            init;
        }

        public string? StartedAt
        {
            get;
            init;
        }

        public WinInetProxySnapshotDocument? Proxy
        {
            get;
            init;
        }
    }

    private sealed class WinInetProxySnapshotDocument
    {
        public int? ProxyEnable
        {
            get;
            init;
        }

        public string? ProxyServer
        {
            get;
            init;
        }

        public string? ProxyOverride
        {
            get;
            init;
        }

        public string? AutoConfigUrl
        {
            get;
            init;
        }

        public int? AutoDetect
        {
            get;
            init;
        }
    }
}
