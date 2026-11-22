using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoSessionTests
{
    private static readonly TimeZoneInfo _zone = TimeZoneInfo.CreateCustomTimeZone(
        "jiumi-test",
        TimeSpan.FromHours(8),
        "jiumi-test",
        "jiumi-test");

    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero),
        _zone);
    private readonly WeChatVideoSession _session;

    public WeChatVideoSessionTests()
    {
        _session = new WeChatVideoSession(_timeProvider);
    }

    [Fact]
    public void AddOrUpdate_GivenNewItems_ListsThemNewestFirst()
    {
        WeChatVideoCaptureItem first = Capture("https://finder.video.qq.com/a?token=1", "oid-1", "nid-1");
        WeChatVideoCaptureItem second = Capture("https://finder.video.qq.com/b?token=2", "oid-2", "nid-2");

        _session.AddOrUpdate(first);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        _session.AddOrUpdate(second);

        IReadOnlyList<WeChatVideoItem> items = _session.Items;
        Assert.Equal(2, items.Count);
        Assert.Equal(second.Url, items[0].Capture.Url);
        Assert.Equal(first.Url, items[1].Capture.Url);
        Assert.Equal(
            new DateTimeOffset(2026, 11, 16, 8, 1, 0, TimeSpan.FromHours(8)),
            items[0].CapturedAt);
    }

    [Fact]
    public void AddOrUpdate_GivenSameOidAndNid_UpdatesTheCaptureInPlace()
    {
        WeChatVideoCaptureItem first = Capture("https://finder.video.qq.com/a?token=1", "oid-1", "nid-1");
        WeChatVideoCaptureItem other = Capture("https://finder.video.qq.com/b?token=2", "oid-2", "nid-2");
        WeChatVideoCaptureItem refreshed = Capture("https://finder.video.qq.com/a?token=3", "oid-1", "nid-1");
        var changes = new List<WeChatVideoSessionChange>();
        _session.Changed += (_, args) => changes.Add(args.Change);
        WeChatVideoItem added = _session.AddOrUpdate(first);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));
        _session.AddOrUpdate(other);

        WeChatVideoItem updated = _session.AddOrUpdate(refreshed);

        Assert.Equal(added.Key, updated.Key);
        Assert.Equal(
            [
                WeChatVideoSessionChange.Added,
                WeChatVideoSessionChange.Added,
                WeChatVideoSessionChange.Updated,
            ],
            changes);
        Assert.Equal(2, _session.Items.Count);
        Assert.Equal(refreshed.Url, _session.Items[1].Capture.Url);
        Assert.Equal(added.CapturedAt, _session.Items[1].CapturedAt);
    }

    [Fact]
    public void AddOrUpdate_GivenUrlsDifferingOnlyByToken_DeduplicatesByUrl()
    {
        WeChatVideoCaptureItem first = Capture("https://finder.video.qq.com/a?token=1&x=1");
        WeChatVideoCaptureItem second = Capture("https://finder.video.qq.com/a?x=1&token=2");

        _session.AddOrUpdate(first);
        _session.AddOrUpdate(second);

        WeChatVideoItem item = Assert.Single(_session.Items);
        Assert.Equal(second.Url, item.Capture.Url);
    }

    [Fact]
    public void AddOrUpdate_GivenDifferentNid_AddsBothItems()
    {
        _session.AddOrUpdate(Capture("https://finder.video.qq.com/a?token=1", "oid-1", "nid-1"));
        _session.AddOrUpdate(Capture("https://finder.video.qq.com/b?token=2", "oid-1", "nid-2"));

        Assert.Equal(2, _session.Items.Count);
    }

    [Fact]
    public void AddLocal_GivenNewFeed_AddsACompletedLocalItem()
    {
        WeChatVideoItem item = _session.AddLocal(
            "feed-1",
            @"C:\videos\clip.mp4",
            "缓存视频 feed-1",
            1234);

        Assert.True(item.IsLocal);
        Assert.Equal(WeChatVideoDownloadState.Completed, item.State);
        Assert.Equal(@"C:\videos\clip.mp4", item.SavedPath);
        Assert.Equal(1234, item.TotalBytes);
        Assert.Equal("缓存视频 feed-1", item.Capture.Description);
        Assert.Same(item, Assert.Single(_session.Items));
    }

    [Fact]
    public void AddLocal_GivenExistingFeed_UpdatesThePathInPlace()
    {
        WeChatVideoItem first = _session.AddLocal(
            "feed-1",
            @"C:\videos\first.mp4",
            "缓存视频 feed-1",
            1234);
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        WeChatVideoItem updated = _session.AddLocal(
            "feed-1",
            @"C:\videos\second.mp4",
            "缓存视频 feed-1",
            5678);

        Assert.Equal(first.Key, updated.Key);
        Assert.Equal(first.CapturedAt, updated.CapturedAt);
        Assert.Equal(@"C:\videos\second.mp4", updated.SavedPath);
        Assert.Equal(5678, updated.TotalBytes);
        Assert.Single(_session.Items);
    }

    [Fact]
    public void Remove_GivenExistingKey_RemovesAndRaises()
    {
        WeChatVideoItem item = _session.AddOrUpdate(
            Capture("https://finder.video.qq.com/a?token=1", "oid-1", "nid-1"));
        WeChatVideoSessionChangedEventArgs? removed = null;
        _session.Changed += (_, args) => removed = args;

        bool result = _session.Remove(item.Key);

        Assert.True(result);
        Assert.Empty(_session.Items);
        Assert.NotNull(removed);
        Assert.Equal(WeChatVideoSessionChange.Removed, removed.Change);
        Assert.Equal(item.Key, removed.Item.Key);
        Assert.False(_session.Remove(item.Key));
    }

    [Fact]
    public void TryUpdate_GivenExistingKey_ReplacesTheItemAndRaises()
    {
        WeChatVideoItem item = _session.AddOrUpdate(
            Capture("https://finder.video.qq.com/a?token=1", "oid-1", "nid-1"));
        WeChatVideoSessionChangedEventArgs? changed = null;
        _session.Changed += (_, args) => changed = args;

        bool result = _session.TryUpdate(
            item.Key,
            current => current with
            {
                State = WeChatVideoDownloadState.Downloading,
                DownloadedBytes = 1024,
            },
            out WeChatVideoItem? updated);

        Assert.True(result);
        Assert.NotNull(updated);
        Assert.Equal(WeChatVideoDownloadState.Downloading, updated.State);
        Assert.Equal(1024, updated.DownloadedBytes);
        Assert.Equal(WeChatVideoDownloadState.Downloading, Assert.Single(_session.Items).State);
        Assert.NotNull(changed);
        Assert.Equal(WeChatVideoSessionChange.Updated, changed.Change);
    }

    [Fact]
    public void TryUpdate_GivenMissingKey_ReturnsFalseWithoutRaising()
    {
        bool raised = false;
        _session.Changed += (_, _) => raised = true;

        bool result = _session.TryUpdate(
            "missing",
            current => current with { State = WeChatVideoDownloadState.Failed },
            out WeChatVideoItem? updated);

        Assert.False(result);
        Assert.Null(updated);
        Assert.False(raised);
    }

    private static WeChatVideoCaptureItem Capture(string url, string? oid = null, string? nid = null) => new()
    {
        Url = url,
        PageUrl = "https://channels.weixin.qq.com/web/pages/feed",
        Oid = oid,
        Nid = nid,
    };

}
