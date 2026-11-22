using JiuMi.Application.WeChatVideo;
using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

public sealed class WeChatPreloadCacheImporterTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "jiumi-184-" + Guid.NewGuid().ToString("N"));

    public WeChatPreloadCacheImporterTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task ImportAsync_GivenCompleteCache_DecryptsTheHeadAndWritesTheVideo()
    {
        const ulong seed = 340230848;
        byte[] plain = CreateMp4Header(256);
        string output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        WriteRecord(seed, plain.Length, plain.Length, CreateEncrypted(plain, seed));

        WeChatPreloadImportResult result = await CreateImporter().ImportAsync(output, "cache");

        Assert.Equal(1, result.Imported);
        Assert.Equal(0, result.Partial);
        Assert.Equal(0, result.Failed);
        string path = Assert.Single(result.ImportedVideos).SavedPath;
        Assert.Equal(plain, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task ImportAsync_GivenPartialCache_SkipsItWithoutWritingAFile()
    {
        const ulong seed = 2099591258;
        byte[] plain = CreateMp4Header(256);
        string output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        WriteRecord(seed, plain.Length, plain.Length + 1024, CreateEncrypted(plain, seed));

        WeChatPreloadImportResult result = await CreateImporter().ImportAsync(output, "cache");

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Partial);
        Assert.Empty(result.ImportedVideos);
        Assert.Empty(Directory.GetFiles(output, "*.mp4"));
    }

    [Fact]
    public async Task ImportAsync_GivenLongVideo_DecryptsOnlyTheHead()
    {
        const ulong seed = 123456789;
        byte[] plain = CreateMp4Header(200000);
        for (int index = 131072; index < plain.Length; index++)
        {
            plain[index] = 0xAB;
        }

        string output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        WriteRecord(seed, plain.Length, plain.Length, CreateEncrypted(plain, seed));

        WeChatPreloadImportResult result = await CreateImporter().ImportAsync(output, "cache");

        string path = Assert.Single(result.ImportedVideos).SavedPath;
        Assert.Equal(plain, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task ImportAsync_GivenExistingOutput_SkipsTheDuplicate()
    {
        const ulong seed = 987654321;
        byte[] plain = CreateMp4Header(256);
        string output = Path.Combine(_root, "output");
        Directory.CreateDirectory(output);
        WriteRecord(seed, plain.Length, plain.Length, CreateEncrypted(plain, seed));
        WeChatPreloadCacheImporter importer = CreateImporter();
        await importer.ImportAsync(output, "cache");

        WeChatPreloadImportResult result = await importer.ImportAsync(output, "cache");

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Empty(result.ImportedVideos);
    }

    private WeChatPreloadCacheImporter CreateImporter()
    {
        return new WeChatPreloadCacheImporter(Logger.None, Path.Combine(_root, "radium"));
    }

    private void WriteRecord(ulong seed, int preloadedLength, int videoLength, byte[] encrypted)
    {
        string radium = Path.Combine(_root, "radium");
        string database = Path.Combine(
            radium,
            "web",
            "profiles",
            "multitab_test",
            "IndexedDB",
            "weixin_xworker_0.indexeddb.leveldb");
        Directory.CreateDirectory(database);
        string json = $$"""
            {"createTime":1,"feedId":"123","videoKey":"finder\\finder_videoCache_test","preDownloadedContentLen":{{preloadedLength}},"videoContentLen":{{videoLength}},"seed":"{{seed}}"}
            """;
        File.WriteAllText(Path.Combine(database, "000003.log"), json);

        string finder = Path.Combine(
            radium,
            "users",
            "test",
            "xworker",
            "liteapp",
            "local",
            "assets",
            "tmp",
            "package",
            "finder");
        Directory.CreateDirectory(finder);
        File.WriteAllBytes(Path.Combine(finder, "finder_videoCache_test"), encrypted);
    }

    private static byte[] CreateMp4Header(int length)
    {
        byte[] data = new byte[length];
        byte[] header =
        [
            0x00, 0x00, 0x00, 0x18,
            0x66, 0x74, 0x79, 0x70,
            0x69, 0x73, 0x6F, 0x6D,
        ];
        header.CopyTo(data, 0);
        return data;
    }

    private static byte[] CreateEncrypted(byte[] plain, ulong seed)
    {
        byte[] encrypted = [.. plain];
        byte[] mask = new Isaac64KeyStream(seed).CreateDecryptionArray(
            Math.Min(131072, encrypted.Length));
        for (int index = 0; index < mask.Length; index++)
        {
            encrypted[index] ^= mask[index];
        }

        return encrypted;
    }
}
