using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatPlaybackCorrelatorTests
{
    private const string PrefixBaseline = "00000000000000000000000000000000";
    private const string PrefixPreloaded = "11111111111111111111111111111111";
    private const string PrefixPlayed = "22222222222222222222222222222222";

    [Fact]
    public void FilterNewEncFileKeys_GivenBaselineKeys_IgnoresBaselineAndReturnsOnlyNew()
    {
        var cache = new WeChatMetadataCache();
        var correlator = new WeChatPlaybackCorrelator(cache);
        correlator.SetBaseline([PrefixBaseline]);

        IReadOnlyList<string> first = correlator.FilterNewEncFileKeys([PrefixBaseline, PrefixPlayed]);
        IReadOnlyList<string> second = correlator.FilterNewEncFileKeys([PrefixBaseline, PrefixPlayed]);

        Assert.Equal([PrefixPlayed], first);
        Assert.Empty(second);
    }

    [Fact]
    public void TryResolve_GivenCachedPreloadItems_EmitsOnlyRequestedPlayedItemOnce()
    {
        var cache = new WeChatMetadataCache();
        cache.Upsert(CreateItem(PrefixPreloaded, "111"));
        WeChatVideoCaptureItem playedItem = CreateItem(PrefixPlayed, "222");
        cache.Upsert(playedItem);

        var correlator = new WeChatPlaybackCorrelator(cache);

        bool first = correlator.TryResolveFromCache(PrefixPlayed, out WeChatVideoCaptureItem? resolved);
        bool second = correlator.TryResolveFromCache(PrefixPlayed, out WeChatVideoCaptureItem? duplicate);

        Assert.True(first);
        Assert.Same(playedItem, resolved);
        Assert.False(second);
        Assert.Null(duplicate);
    }

    [Fact]
    public void TryMarkEmitted_GivenTargetedFallbackItem_CachesAndPreventsDuplicateEmission()
    {
        var cache = new WeChatMetadataCache();
        var correlator = new WeChatPlaybackCorrelator(cache);
        WeChatVideoCaptureItem item = CreateItem(PrefixPlayed, "222");

        Assert.True(correlator.TryAcceptResolved(PrefixPlayed, item));
        Assert.False(correlator.TryAcceptResolved(PrefixPlayed, item));
        Assert.True(cache.TryGet(PrefixPlayed, out WeChatVideoCaptureItem? cached));
        Assert.Same(item, cached);
    }

    [Fact]
    public void Reset_ClearsBaselineAndEmittedHistory()
    {
        var cache = new WeChatMetadataCache();
        var correlator = new WeChatPlaybackCorrelator(cache);
        correlator.SetBaseline([PrefixBaseline]);
        Assert.Empty(correlator.FilterNewEncFileKeys([PrefixBaseline]));

        correlator.Reset();

        Assert.Equal([PrefixBaseline], correlator.FilterNewEncFileKeys([PrefixBaseline]));
    }

    [Fact]
    public void FilterNewPlayedFeedIds_GivenBaselineAndMapping_ResolvesOnlyNewlyPlayedFeed()
    {
        var cache = new WeChatMetadataCache();
        var correlator = new WeChatPlaybackCorrelator(cache);
        correlator.SetPlayedFeedBaseline(["15000000000000000001"]);
        correlator.RecordFeedMapping("15000000000000000002", PrefixPlayed);

        IReadOnlyList<string> newFeeds = correlator.FilterNewPlayedFeedIds(
            ["15000000000000000001", "15000000000000000002"]);

        Assert.Equal(["15000000000000000002"], newFeeds);
        Assert.True(correlator.TryGetEncFileKeyPrefix("15000000000000000002", out string? mapped));
        Assert.Equal(PrefixPlayed, mapped);
    }

    private static WeChatVideoCaptureItem CreateItem(string prefix, string decodeKey) => new()
    {
        Url = $"https://finder.video.qq.com/251/20302/stodownload?encfilekey={prefix}rest&token=tok",
        DecodeKey = decodeKey,
    };
}
