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
    private const int EncryptedHeadLength = 131072;

    private static readonly Regex _recordPattern = new(
        "\\{\"createTime\":(?<created>[0-9]+),\"feedId\":\"(?<feed>[^\"]*)\",\"videoKey\":\"(?<key>(?:[^\"\\\\]|\\\\.)*)\",\"preDownloadedContentLen\":(?<pre>[0-9]+),\"videoContentLen\":(?<total>[0-9]+),\"seed\":\"(?<seed>[0-9]*)\"\\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly Serilog.ILogger _logger;
    private readonly string _radiumRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatPreloadCacheImporter"/> class.
    /// </summary>
    /// <param name="logger">Receives per-file diagnostics without seeds or media tokens.</param>
    public WeChatPreloadCacheImporter(Serilog.ILogger logger)
        : this(logger, DefaultRadiumRoot())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatPreloadCacheImporter"/> class with an
    /// explicit Weixin data root.
    /// </summary>
    /// <param name="logger">Receives per-file diagnostics without seeds or media tokens.</param>
    /// <param name="radiumRoot">The Weixin <c>radium</c> directory.</param>
    public WeChatPreloadCacheImporter(Serilog.ILogger logger, string radiumRoot)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(radiumRoot);
        _logger = logger;
        _radiumRoot = radiumRoot;
    }

    /// <inheritdoc />
    public async Task<WeChatPreloadImportResult> ImportAsync(
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException(outputDirectory);
        }

        int imported = 0;
        int partial = 0;
        int failed = 0;
        List<string> saved = [];

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
            if (actualLength < record.VideoContentLength)
            {
                partial++;
                continue;
            }

            try
            {
                byte[] data = await File.ReadAllBytesAsync(cachePath, cancellationToken).ConfigureAwait(false);
                byte[] mask = new Isaac64KeyStream(record.Seed).CreateDecryptionArray(
                    Math.Min(EncryptedHeadLength, data.Length));
                for (int index = 0; index < mask.Length; index++)
                {
                    data[index] ^= mask[index];
                }

                string stem = WeChatVideoFileName.CreateStem(
                    $"视频号缓存 {record.FeedId}",
                    DateTimeOffset.Now);
                string path = Path.Combine(
                    outputDirectory,
                    stem + WeChatVideoFileName.Extension);
                if (File.Exists(path))
                {
                    imported++;
                    saved.Add(path);
                    continue;
                }

                await File.WriteAllBytesAsync(path, data, cancellationToken).ConfigureAwait(false);
                imported++;
                saved.Add(path);
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

        return new WeChatPreloadImportResult(imported, partial, failed, saved);
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
            string feedId = root.GetProperty("feedId").GetString() ?? string.Empty;
            string videoKey = root.GetProperty("videoKey").GetString() ?? string.Empty;
            string seedText = root.GetProperty("seed").GetString() ?? string.Empty;
            if (videoKey.Length == 0 ||
                !ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
            {
                return null;
            }

            return new PreloadRecord(
                root.GetProperty("createTime").GetInt64(),
                feedId,
                videoKey,
                root.GetProperty("preDownloadedContentLen").GetInt64(),
                root.GetProperty("videoContentLen").GetInt64(),
                seed);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (KeyNotFoundException)
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
