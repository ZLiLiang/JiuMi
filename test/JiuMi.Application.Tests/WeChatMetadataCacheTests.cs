using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatMetadataCacheTests
{
    [Fact]
    public void Upsert_GivenNewItem_RetrievesByEncFileKeyPrefix()
    {
        var cache = new WeChatMetadataCache();
        string prefix = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        WeChatVideoCaptureItem item = CreateItem(prefix, decodeKey: "123", token: "tok1");

        cache.Upsert(item);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet(prefix, out WeChatVideoCaptureItem? found));
        Assert.Same(item, found);
    }

    [Fact]
    public void Upsert_GivenCapacityExceeded_EvictsLeastRecentlyUsed()
    {
        var cache = new WeChatMetadataCache(capacity: 2);
        string p1 = "11111111111111111111111111111111";
        string p2 = "22222222222222222222222222222222";
        string p3 = "33333333333333333333333333333333";

        cache.Upsert(CreateItem(p1, "1", "t1"));
        cache.Upsert(CreateItem(p2, "2", "t2"));

        // Touch p1 so p2 becomes LRU
        Assert.True(cache.TryGet(p1, out _));

        cache.Upsert(CreateItem(p3, "3", "t3"));

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGet(p1, out _));
        Assert.False(cache.TryGet(p2, out _));
        Assert.True(cache.TryGet(p3, out _));
    }

    [Fact]
    public void Upsert_GivenLessCompleteDuplicate_KeepsRicherExistingMetadata()
    {
        var cache = new WeChatMetadataCache();
        string prefix = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        WeChatVideoCaptureItem rich = CreateItem(prefix, decodeKey: "123", token: "tok1", description: "标题");
        WeChatVideoCaptureItem poor = CreateItem(prefix, decodeKey: "123", token: null, description: null);

        cache.Upsert(rich);
        cache.Upsert(poor);

        Assert.True(cache.TryGet(prefix, out WeChatVideoCaptureItem? found));
        Assert.Same(rich, found);
    }

    [Fact]
    public void Upsert_GivenMoreCompleteDuplicate_ReplacesExistingMetadata()
    {
        var cache = new WeChatMetadataCache();
        string prefix = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        WeChatVideoCaptureItem initial = CreateItem(prefix, decodeKey: "123", token: null, description: null);
        WeChatVideoCaptureItem richer = CreateItem(prefix, decodeKey: "123", token: "tok1", description: "标题");

        cache.Upsert(initial);
        cache.Upsert(richer);

        Assert.True(cache.TryGet(prefix, out WeChatVideoCaptureItem? found));
        Assert.Same(richer, found);
    }

    [Fact]
    public void Clear_RemovesAllCachedItems()
    {
        var cache = new WeChatMetadataCache();
        string prefix = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        cache.Upsert(CreateItem(prefix, "123", "tok1"));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet(prefix, out _));
    }

    [Fact]
    public void Upsert_GivenDuplicateWithoutSpecs_KeepsExistingItemWithSpecs()
    {
        var cache = new WeChatMetadataCache();
        string prefix = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        WeChatVideoCaptureItem withSpecs = CreateItem(prefix, decodeKey: "123", token: "tok1", description: "标题") with
        {
            Specs =
            [
                new WeChatVideoSpec { FileFormat = "xWT101", Width = 1080, Height = 1920, CodingFormat = "h265" },
            ],
        };
        WeChatVideoCaptureItem withoutSpecs = CreateItem(prefix, decodeKey: "123", token: "tok1", description: "标题");

        cache.Upsert(withSpecs);
        cache.Upsert(withoutSpecs);

        Assert.True(cache.TryGet(prefix, out WeChatVideoCaptureItem? found));
        Assert.Same(withSpecs, found);
    }

    private static WeChatVideoCaptureItem CreateItem(
        string prefix,
        string? decodeKey,
        string? token,
        string? description = null)
    {
        string url = $"https://finder.video.qq.com/251/20302/stodownload?encfilekey={prefix}rest";
        if (!string.IsNullOrEmpty(token))
        {
            url += "&token=" + token;
        }

        return new WeChatVideoCaptureItem
        {
            Url = url,
            DecodeKey = decodeKey,
            Description = description,
        };
    }
}
