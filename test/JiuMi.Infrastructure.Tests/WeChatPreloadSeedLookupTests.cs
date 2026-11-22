using JiuMi.Application.WeChatVideo;
using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

public sealed class WeChatPreloadSeedLookupTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "jiumi-184-seed-" + Guid.NewGuid().ToString("N"));

    public WeChatPreloadSeedLookupTests()
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
    public void Find_GivenSameLength_ReturnsTheClosestRecord()
    {
        DateTimeOffset capturedAt = DateTimeOffset.FromUnixTimeMilliseconds(2_000_000);
        WriteRecords(
            Record(1000, 2_000_000 - 60_000, 500_000, 11),
            Record(1001, 2_000_000 - 1_000, 500_000, 22),
            Record(1002, 2_000_000 - 1_000, 600_000, 33));

        WeChatPreloadSeed? seed = CreateLookup().Find(500_000, capturedAt);

        Assert.NotNull(seed);
        Assert.Equal("1001", seed.FeedId);
        Assert.Equal(22UL, seed.Seed);
    }

    [Fact]
    public void Find_GivenNoMatchingLength_ReturnsNull()
    {
        WriteRecords(Record(1000, 2_000_000, 500_000, 11));

        Assert.Null(CreateLookup().Find(600_000, DateTimeOffset.FromUnixTimeMilliseconds(2_000_000)));
    }

    [Fact]
    public void Find_GivenStaleRecord_ReturnsNull()
    {
        WriteRecords(Record(1000, 1_000_000, 500_000, 11));

        Assert.Null(CreateLookup().Find(500_000, DateTimeOffset.FromUnixTimeMilliseconds(28_800_000)));
    }

    private WeChatPreloadSeedLookup CreateLookup()
    {
        return new WeChatPreloadSeedLookup(Logger.None, Path.Combine(_root, "radium"));
    }

    private void WriteRecords(params string[] records)
    {
        string database = Path.Combine(
            _root,
            "radium",
            "web",
            "profiles",
            "multitab_test",
            "IndexedDB",
            "weixin_xworker_0.indexeddb.leveldb");
        Directory.CreateDirectory(database);
        File.WriteAllText(Path.Combine(database, "000003.log"), string.Join('\n', records));
    }

    private static string Record(long feedId, long createTime, long videoContentLength, ulong seed)
    {
        return $$"""
            {"createTime":{{createTime}},"feedId":"{{feedId}}","videoKey":"finder\\finder_videoCache_{{feedId}}","preDownloadedContentLen":524288,"videoContentLen":{{videoContentLength}},"seed":"{{seed}}"}
            """;
    }
}
