using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Reads LiteApp's preload records from the xweb IndexedDB log and writes every complete cached
/// video after decrypting its head.
/// </summary>
public sealed class WeChatPreloadCacheImporter : IWeChatPreloadCacheImporter
{
    private static readonly Regex _recordPattern = new(
        "\\{\"createTime\":(?<created>[0-9]+),\"feedId\":\"(?<feed>[^\"]*)\",\"videoKey\":\"(?<key>(?:[^\"\\\\]|\\\\.)*)\",\"preDownloadedContentLen\":(?<pre>[0-9]+),\"videoContentLen\":(?<total>[0-9]+),\"seed\":\"(?<seed>[0-9]*)\"\\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly Serilog.ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly string _radiumRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatPreloadCacheImporter"/> class.
    /// </summary>
    /// <param name="logger">Receives per-file diagnostics without seeds or media tokens.</param>
    /// <param name="timeProvider">Supplies the output file timestamp.</param>
    public WeChatPreloadCacheImporter(Serilog.ILogger logger, TimeProvider timeProvider)
        : this(logger, timeProvider, DefaultRadiumRoot())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatPreloadCacheImporter"/> class with an
    /// explicit Weixin data root.
    /// </summary>
    /// <param name="logger">Receives per-file diagnostics without seeds or media tokens.</param>
    /// <param name="radiumRoot">The Weixin <c>radium</c> directory.</param>
    public WeChatPreloadCacheImporter(Serilog.ILogger logger, string radiumRoot)
        : this(logger, TimeProvider.System, radiumRoot)
    {
    }

    private WeChatPreloadCacheImporter(
        Serilog.ILogger logger,
        TimeProvider timeProvider,
        string radiumRoot)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(radiumRoot);
        _logger = logger;
        _timeProvider = timeProvider;
        _radiumRoot = radiumRoot;
    }

    /// <inheritdoc />
    public async Task<WeChatPreloadImportResult> ImportAsync(
        string outputDirectory,
        string fileStemPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileStemPrefix);
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException(outputDirectory);
        }

        int imported = 0;
        int partial = 0;
        int failed = 0;
        int skipped = 0;
        List<WeChatPreloadImportedVideo> importedVideos = [];

        foreach (PreloadRecord record in ReadRecords())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? cachePath = FindCacheFile(record.VideoKey);
            if (cachePath is null)
            {
                failed++;
                _logger.Debug("A LiteApp preload file is missing for feed {FeedId}", record.FeedId);
                continue;
            }

            long actualLength = new FileInfo(cachePath).Length;
            if (record.PreDownloadedContentLength < record.VideoContentLength ||
                actualLength < record.VideoContentLength)
            {
                partial++;
                continue;
            }

            try
            {
                byte[] data = await File.ReadAllBytesAsync(cachePath, cancellationToken).ConfigureAwait(false);
                Isaac64KeyStream.DecryptHead(data, record.Seed);
                if (!HasMp4Header(data))
                {
                    failed++;
                    _logger.Debug("A LiteApp preload did not decrypt to an MP4 for feed {FeedId}", record.FeedId);
                    continue;
                }

                string stem = WeChatVideoFileName.CreateStem(
                    $"{fileStemPrefix} {record.FeedId}",
                    _timeProvider.GetLocalNow());
                string path = Path.Combine(
                    outputDirectory,
                    stem + WeChatVideoFileName.Extension);
                if (File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                await File.WriteAllBytesAsync(path, data, cancellationToken).ConfigureAwait(false);
                imported++;
                importedVideos.Add(new WeChatPreloadImportedVideo(record.FeedId, path, data.Length));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failed++;
                _logger.Warning(
                    exception,
                    "Importing the LiteApp preload for feed {FeedId} failed",
                    record.FeedId);
            }
        }

        return new WeChatPreloadImportResult(
            imported,
            partial,
            failed,
            skipped,
            importedVideos);
    }

    private static bool HasMp4Header(byte[] data)
    {
        return data.Length >= 8 &&
            data[4] == (byte)'f' &&
            data[5] == (byte)'t' &&
            data[6] == (byte)'y' &&
            data[7] == (byte)'p';
    }

    private IReadOnlyList<PreloadRecord> ReadRecords()
    {
        Dictionary<string, PreloadRecord> records = new(StringComparer.Ordinal);
        foreach (string file in EnumerateRecordFiles())
        {
            string text;
            try
            {
                text = File.ReadAllText(file, Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.Debug(exception, "Reading a LiteApp preload record file failed");
                continue;
            }

            foreach (Match match in _recordPattern.Matches(text))
            {
                PreloadRecord? record = ParseRecord(match.Value);
                if (record is null)
                {
                    continue;
                }

                if (!records.TryGetValue(record.VideoKey, out PreloadRecord? existing) ||
                    record.CreateTime > existing.CreateTime)
                {
                    records[record.VideoKey] = record;
                }
            }
        }

        return [.. records.Values];
    }

    private static PreloadRecord? ParseRecord(string value)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("feedId", out JsonElement feedElement) ||
                feedElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("videoKey", out JsonElement keyElement) ||
                keyElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("seed", out JsonElement seedElement) ||
                seedElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("createTime", out JsonElement createdElement) ||
                !createdElement.TryGetInt64(out long createTime) ||
                !root.TryGetProperty("preDownloadedContentLen", out JsonElement preloadedElement) ||
                !preloadedElement.TryGetInt64(out long preloadedLength) ||
                !root.TryGetProperty("videoContentLen", out JsonElement totalElement) ||
                !totalElement.TryGetInt64(out long totalLength))
            {
                return null;
            }

            string feedId = feedElement.GetString() ?? string.Empty;
            string videoKey = keyElement.GetString() ?? string.Empty;
            string seedText = seedElement.GetString() ?? string.Empty;
            if (videoKey.Length == 0 ||
                !ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
            {
                return null;
            }

            return new PreloadRecord(
                createTime,
                feedId,
                videoKey,
                preloadedLength,
                totalLength,
                seed);
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

    private string? FindCacheFile(string videoKey)
    {
        string fileName = Path.GetFileName(videoKey.Replace('\\', '/'));
        string users = Path.Combine(_radiumRoot, "users");
        if (!Directory.Exists(users))
        {
            return null;
        }

        foreach (string user in Directory.EnumerateDirectories(users))
        {
            string temporary = Path.Combine(
                user,
                "xworker",
                "liteapp",
                "local",
                "assets",
                "tmp");
            if (!Directory.Exists(temporary))
            {
                continue;
            }

            foreach (string package in Directory.EnumerateDirectories(temporary))
            {
                string candidate = Path.Combine(package, "finder", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string DefaultRadiumRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tencent",
            "xwechat",
            "radium");
    }

    private sealed record PreloadRecord(
        long CreateTime,
        string FeedId,
        string VideoKey,
        long PreDownloadedContentLength,
        long VideoContentLength,
        ulong Seed);
}
