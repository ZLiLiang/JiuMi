using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoDownloadCoordinatorTests
{
    private static readonly TimeZoneInfo _zone = TimeZoneInfo.CreateCustomTimeZone(
        "jiumi-test",
        TimeSpan.FromHours(8),
        "jiumi-test",
        "jiumi-test");

    private readonly ManualTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 11, 16, 0, 9, 10, TimeSpan.Zero),
        _zone);
    private readonly WeChatVideoSession _session;

    public WeChatVideoDownloadCoordinatorTests()
    {
        _session = new WeChatVideoSession(_timeProvider);
    }

    [Fact]
    public async Task StartAsync_GivenCompletedDownload_UpdatesTheSessionItem()
    {
        var downloader = new StubDownloader();
        await using var queue = new WeChatVideoDownloadQueue(downloader);
        var coordinator = new WeChatVideoDownloadCoordinator(_session, queue);
        WeChatVideoItem item = AddItem("描述", decodeKey: "42");
        downloader.Handler = (_, _) => Task.FromResult(
            WeChatVideoDownloadResult.Success(@"C:\out\video.mp4", 200, 200));

        WeChatVideoDownloadResult result = await coordinator.StartAsync(item, Path.GetTempPath());

        Assert.Equal(WeChatVideoDownloadStatus.Completed, result.Status);
        WeChatVideoItem updated = Assert.Single(_session.Items);
        Assert.Equal(WeChatVideoDownloadState.Completed, updated.State);
        Assert.Equal(@"C:\out\video.mp4", updated.SavedPath);
        Assert.Equal(200, updated.DownloadedBytes);
        Assert.Equal(200, updated.TotalBytes);
        Assert.Equal(WeChatVideoDownloadFailure.None, updated.Failure);
        Assert.NotNull(downloader.LastRequest);
        Assert.Equal("描述", downloader.LastRequest.FileStem);
        Assert.Equal("42", downloader.LastRequest.DecodeKey);
        Assert.Equal(item.Capture.Url, downloader.LastRequest.MediaUrl.AbsoluteUri);
    }

    [Fact]
    public async Task StartAsync_GivenExpiredDownload_MarksTheItemExpired()
    {
        var downloader = new StubDownloader
        {
            Handler = (_, _) => Task.FromResult(WeChatVideoDownloadResult.Expired(50, 100)),
        };
        await using var queue = new WeChatVideoDownloadQueue(downloader);
        var coordinator = new WeChatVideoDownloadCoordinator(_session, queue);
        WeChatVideoItem item = AddItem("描述");

        WeChatVideoDownloadResult result = await coordinator.StartAsync(item, Path.GetTempPath());

        Assert.Equal(WeChatVideoDownloadStatus.Expired, result.Status);
        WeChatVideoItem updated = Assert.Single(_session.Items);
        Assert.Equal(WeChatVideoDownloadState.Expired, updated.State);
        Assert.Equal(50, updated.DownloadedBytes);
        Assert.Equal(100, updated.TotalBytes);
    }

    [Fact]
    public async Task StartAsync_GivenFailedDownload_MarksTheItemFailedWithTheReason()
    {
        var downloader = new StubDownloader
        {
            Handler = (_, _) => Task.FromResult(
                WeChatVideoDownloadResult.Failed(WeChatVideoDownloadFailure.Network, 10, 100)),
        };
        await using var queue = new WeChatVideoDownloadQueue(downloader);
        var coordinator = new WeChatVideoDownloadCoordinator(_session, queue);
        WeChatVideoItem item = AddItem("描述");

        await coordinator.StartAsync(item, Path.GetTempPath());

        WeChatVideoItem updated = Assert.Single(_session.Items);
        Assert.Equal(WeChatVideoDownloadState.Failed, updated.State);
        Assert.Equal(WeChatVideoDownloadFailure.Network, updated.Failure);
    }

    [Fact]
    public async Task StartAsync_GivenCanceledDownload_MarksTheItemCanceled()
    {
        var downloader = new StubDownloader
        {
            Handler = (_, _) => Task.FromResult(WeChatVideoDownloadResult.Canceled(10, 100)),
        };
        await using var queue = new WeChatVideoDownloadQueue(downloader);
        var coordinator = new WeChatVideoDownloadCoordinator(_session, queue);
        WeChatVideoItem item = AddItem("描述");

        await coordinator.StartAsync(item, Path.GetTempPath());

        WeChatVideoItem updated = Assert.Single(_session.Items);
        Assert.Equal(WeChatVideoDownloadState.Canceled, updated.State);
        Assert.Equal(10, updated.DownloadedBytes);
    }

    [Fact]
    public async Task StartAsync_GivenEmptyDescription_UsesTheTimestampStem()
    {
        var downloader = new StubDownloader
        {
            Handler = (_, _) => Task.FromResult(
                WeChatVideoDownloadResult.Success(@"C:\out\video.mp4", 1, 1)),
        };
        await using var queue = new WeChatVideoDownloadQueue(downloader);
        var coordinator = new WeChatVideoDownloadCoordinator(_session, queue);
        WeChatVideoItem item = AddItem(description: null);

        await coordinator.StartAsync(item, Path.GetTempPath());

        Assert.NotNull(downloader.LastRequest);
        Assert.Equal("视频_20261116_080910", downloader.LastRequest.FileStem);
    }

    [Fact]
    public async Task StartAsync_GivenProgress_ForwardsItAndShowsTheItemDownloading()
    {
        var snapshots = new List<(WeChatVideoDownloadState State, long Bytes)>();
        var values = new List<WeChatVideoDownloadProgress>();
        var downstream = new InlineProgress(value =>
        {
            values.Add(value);
            WeChatVideoItem current = Assert.Single(_session.Items);
            snapshots.Add((current.State, current.DownloadedBytes));
        });
        var downloader = new StubDownloader
        {
            Handler = (_, progress) =>
            {
                progress?.Report(new WeChatVideoDownloadProgress(100, 200));
                return Task.FromResult(
                    WeChatVideoDownloadResult.Success(@"C:\out\video.mp4", 200, 200));
            },
        };
        await using var queue = new WeChatVideoDownloadQueue(downloader);
        var coordinator = new WeChatVideoDownloadCoordinator(_session, queue);
        WeChatVideoItem item = AddItem("描述");

        await coordinator.StartAsync(item, Path.GetTempPath(), downstream);

        Assert.Equal([new WeChatVideoDownloadProgress(100, 200)], values);
        Assert.Equal([(WeChatVideoDownloadState.Downloading, 100L)], snapshots);
    }

    private WeChatVideoItem AddItem(string? description, string? decodeKey = null)
    {
        return _session.AddOrUpdate(new WeChatVideoCaptureItem
        {
            Url = "https://finder.video.qq.com/media?token=abc",
            PageUrl = "https://channels.weixin.qq.com/web/pages/feed?oid=oid-1&nid=nid-1",
            Oid = "oid-1",
            Nid = "nid-1",
            Description = description,
            DecodeKey = decodeKey,
        });
    }

    private sealed class StubDownloader : IWeChatVideoDownloader
    {
        public Func<
            WeChatVideoDownloadRequest,
            IProgress<WeChatVideoDownloadProgress>?,
            Task<WeChatVideoDownloadResult>> Handler
        {
            get; set;
        } =
            (_, _) => Task.FromResult(WeChatVideoDownloadResult.Canceled());

        public WeChatVideoDownloadRequest? LastRequest
        {
            get; private set;
        }

        public Task<WeChatVideoDownloadResult> DownloadAsync(
            WeChatVideoDownloadRequest request,
            IProgress<WeChatVideoDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Handler(request, progress);
        }
    }

    private sealed class InlineProgress : IProgress<WeChatVideoDownloadProgress>
    {
        private readonly Action<WeChatVideoDownloadProgress> _report;

        public InlineProgress(Action<WeChatVideoDownloadProgress> report)
        {
            _report = report;
        }

        public void Report(WeChatVideoDownloadProgress value) => _report(value);
    }

}
