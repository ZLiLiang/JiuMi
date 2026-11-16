using JiuMi.Application.WeChatVideo;

namespace JiuMi.Application.Tests;

public sealed class WeChatVideoDownloadQueueTests
{
    [Fact]
    public async Task EnqueueAsync_GivenSeveralJobs_RunsThemInOrderAndOneAtATime()
    {
        var downloader = new BlockingDownloader();
        await using var queue = new WeChatVideoDownloadQueue(downloader);

        Task<WeChatVideoDownloadResult> first = queue.EnqueueAsync(Request("1"));
        Task<WeChatVideoDownloadResult> second = queue.EnqueueAsync(Request("2"));
        Task<WeChatVideoDownloadResult> third = queue.EnqueueAsync(Request("3"));

        await downloader.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.False(downloader.SecondStarted.Task.IsCompleted);
        downloader.Release.SetResult();

        WeChatVideoDownloadResult[] results = await Task.WhenAll(first, second, third);
        Assert.All(results, result => Assert.Equal(WeChatVideoDownloadStatus.Completed, result.Status));
        Assert.Equal(["1", "2", "3"], downloader.Started);
        Assert.Equal(1, downloader.MaxConcurrency);
    }

    [Fact]
    public async Task EnqueueAsync_GivenCanceledQueuedJob_CompletesItWithoutStarting()
    {
        var downloader = new BlockingDownloader();
        await using var queue = new WeChatVideoDownloadQueue(downloader);
        Task<WeChatVideoDownloadResult> first = queue.EnqueueAsync(Request("1"));
        await downloader.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var source = new CancellationTokenSource();
        Task<WeChatVideoDownloadResult> second = queue.EnqueueAsync(
            Request("2"),
            cancellationToken: source.Token);

        await source.CancelAsync();

        WeChatVideoDownloadResult canceled = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(WeChatVideoDownloadStatus.Canceled, canceled.Status);
        Assert.Equal(["1"], downloader.Started);
        downloader.Release.SetResult();
        await first;
    }

    [Fact]
    public async Task EnqueueAsync_GivenAlreadyCanceledToken_ReturnsCanceledWithoutStarting()
    {
        var downloader = new BlockingDownloader();
        await using var queue = new WeChatVideoDownloadQueue(downloader);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        WeChatVideoDownloadResult result = await queue.EnqueueAsync(
            Request("1"),
            cancellationToken: source.Token);

        Assert.Equal(WeChatVideoDownloadStatus.Canceled, result.Status);
        Assert.Empty(downloader.Started);
    }

    private static WeChatVideoDownloadRequest Request(string stem) => new()
    {
        MediaUrl = new Uri("https://finder.video.qq.com/media"),
        OutputDirectory = Path.GetTempPath(),
        FileStem = stem,
    };

    private sealed class BlockingDownloader : IWeChatVideoDownloader
    {
        private readonly object _gate = new();
        private int _active;

        public TaskCompletionSource FirstStarted
        {
            get;
        } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondStarted
        {
            get;
        } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release
        {
            get;
        } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Started { get; } = [];

        public int MaxConcurrency
        {
            get; private set;
        }

        public async Task<WeChatVideoDownloadResult> DownloadAsync(
            WeChatVideoDownloadRequest request,
            IProgress<WeChatVideoDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            int count;
            lock (_gate)
            {
                _active++;
                MaxConcurrency = Math.Max(MaxConcurrency, _active);
                Started.Add(request.FileStem);
                count = Started.Count;
            }

            if (count == 1)
            {
                FirstStarted.TrySetResult();
            }
            else if (count == 2)
            {
                SecondStarted.TrySetResult();
            }

            try
            {
                if (count == 1)
                {
                    await Release.Task.WaitAsync(cancellationToken);
                }

                return WeChatVideoDownloadResult.Success(request.FileStem, 0, 0);
            }
            finally
            {
                lock (_gate)
                {
                    _active--;
                }
            }
        }
    }
}
