using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Reads LiteApp's preload records from the xweb IndexedDB log and finds the seed for a captured
/// media response by exact full-video length and capture time.
/// </summary>
public sealed class WeChatPreloadSeedLookup : IWeChatPreloadSeedLookup
{
    private static readonly TimeSpan _matchWindow = TimeSpan.FromHours(6);

    private static readonly Regex _recordPattern = new(
        "\\{\"createTime\":(?<created>[0-9]+),\"feedId\":\"(?<feed>[^\"]*)\",\"videoKey\":\"(?<key>(?:[^\"\\\\]|\\\\.)*)\",\"preDownloadedContentLen\":(?<pre>[0-9]+),\"videoContentLen\":(?<total>[0-9]+),\"seed\":\"(?<seed>[0-9]*)\"\\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly Serilog.ILogger _logger;
    private readonly string _radiumRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatPreloadSeedLookup"/> class.
    /// </summary>
    /// <param name="logger">Receives diagnostics without seeds or media tokens.</param>
    public WeChatPreloadSeedLookup(Serilog.ILogger logger)
        : this(logger, DefaultRadiumRoot())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatPreloadSeedLookup"/> class with an
    /// explicit Weixin data root.
    /// </summary>
    /// <param name="logger">Receives diagnostics without seeds or media tokens.</param>
    /// <param name="radiumRoot">The Weixin <c>radium</c> directory.</param>
    public WeChatPreloadSeedLookup(Serilog.ILogger logger, string radiumRoot)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(radiumRoot);
        _logger = logger;
        _radiumRoot = radiumRoot;
    }

    /// <inheritdoc />
    public WeChatPreloadSeed? Find(long videoContentLength, DateTimeOffset capturedAt)
    {
        if (videoContentLength <= 0)
        {
            return null;
        }

        long capturedMilliseconds = capturedAt.ToUnixTimeMilliseconds();
        long windowMilliseconds = (long)_matchWindow.TotalMilliseconds;
        WeChatPreloadSeed? best = null;
        long bestDistance = long.MaxValue;
        foreach (WeChatPreloadSeed record in ReadRecords())
        {
            if (record.VideoContentLength != videoContentLength)
            {
                continue;
            }

            long distance = Math.Abs(record.CreateTime - capturedMilliseconds);
            if (distance <= windowMilliseconds && distance < bestDistance)
            {
                best = record;
                bestDistance = distance;
            }
        }

        if (best is null)
        {
            _logger.Debug(
                "No LiteApp preload seed matched a {Length}-byte media response",
                videoContentLength);
        }

        return best;
    }

    private List<WeChatPreloadSeed> ReadRecords()
    {
        List<WeChatPreloadSeed> records = [];
        foreach (string file in EnumerateRecordFiles())
        {
            string text;
            try
            {
                using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false);
                text = reader.ReadToEnd();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.Debug(exception, "Reading a LiteApp preload record file failed");
                continue;
            }

            foreach (Match match in _recordPattern.Matches(text))
            {
                WeChatPreloadSeed? record = ParseRecord(match.Value);
                if (record is not null)
                {
                    records.Add(record);
                }
            }
        }

        return records;
    }

    private static WeChatPreloadSeed? ParseRecord(string value)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("feedId", out JsonElement feedElement) ||
                feedElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("seed", out JsonElement seedElement) ||
                seedElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("createTime", out JsonElement createdElement) ||
                !createdElement.TryGetInt64(out long createTime) ||
                !root.TryGetProperty("videoContentLen", out JsonElement totalElement) ||
                !totalElement.TryGetInt64(out long totalLength))
            {
                return null;
            }

            string seedText = seedElement.GetString() ?? string.Empty;
            if (!ulong.TryParse(
                    seedText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ulong seed))
            {
                return null;
            }

            return new WeChatPreloadSeed(
                feedElement.GetString() ?? string.Empty,
                seed,
                totalLength,
                createTime);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private IEnumerable<string> EnumerateRecordFiles()
    {
        string profiles = Path.Combine(_radiumRoot, "web", "profiles");
        if (!Directory.Exists(profiles))
        {
            yield break;
        }

        foreach (string profile in Directory.EnumerateDirectories(profiles))
        {
            string database = Path.Combine(
                profile,
                "IndexedDB",
                "weixin_xworker_0.indexeddb.leveldb");
            if (!Directory.Exists(database))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(database))
            {
                if (string.Equals(Path.GetExtension(file), ".log", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetExtension(file), ".ldb", StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }

    private static string DefaultRadiumRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tencent",
            "xwechat",
            "radium");
    }
}
