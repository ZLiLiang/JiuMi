using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using JiuMi.Application.WeChatVideo;
using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

public sealed class MemoryWeChatVideoCaptureTests
{
    private const string BaselineFeedId = "15000000000000000001";
    private const string PlayedFeedId = "15000000000000000002";
    private const string PrebufferedNextFeedId = "15000000000000000003";
    private const string LateFeedId = "15000000000000000004";
    private const string RestartFeedId = "15000000000000000005";

    private const string BaselineKey = "00000000000000000000000000000000base";
    private const string PlayedKey = "11111111111111111111111111111111play";
    private const string PrebufferedNextKey = "22222222222222222222222222222222next";
    private const string LateKey = "33333333333333333333333333333333late";
    private const string RestartKey = "44444444444444444444444444444444rest";

    private static readonly MemoryWeChatVideoCaptureOptions _fastOptions = new(
        MetadataPollInterval: TimeSpan.FromMilliseconds(20),
        PlaybackPollInterval: TimeSpan.FromMilliseconds(15),
        TargetedRetryDelay: TimeSpan.FromMilliseconds(25));

    [Fact]
    public async Task StartAsync_GivenNoWeChatProcess_ThrowsInvalidOperationAndStaysStopped()
    {
        var locator = new FakeProcessLocator();
        var reader = new FakeMemoryReader();
        using var capture = new MemoryWeChatVideoCapture(locator, reader, TimeProvider.System, Logger.None, _fastOptions);

        await Assert.ThrowsAsync<InvalidOperationException>(() => capture.StartAsync());

        Assert.Equal(WeChatVideoCaptureStatus.Stopped, capture.Status);
    }

    [Fact]
    public async Task StartAsync_GivenAccessDeniedOnAllProcesses_ThrowsUnauthorizedAccessAndStaysStopped()
    {
        var locator = new FakeProcessLocator([new WeChatProcessInfo(100, "Weixin", IsNetworkService: false)]);
        var reader = new FakeMemoryReader();
        reader.SetAccessStatus(100, WeChatProcessAccessStatus.AccessDenied);
        using var capture = new MemoryWeChatVideoCapture(locator, reader, TimeProvider.System, Logger.None, _fastOptions);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => capture.StartAsync());

        Assert.Equal(WeChatVideoCaptureStatus.Stopped, capture.Status);
    }

    [Fact]
    public async Task Capture_GivenBaselinePreloadedAndPrebufferedNextVideos_EmitsOnlyVideoPlayStartVideo()
    {
        var locator = new FakeProcessLocator(
        [
            new WeChatProcessInfo(100, "Weixin", IsNetworkService: false),
            new WeChatProcessInfo(200, "WeChatAppEx", IsNetworkService: false),
        ]);
        var reader = new FakeMemoryReader();

        var metadata = new StringBuilder();
        metadata.Append(BuildMediaJson(BaselineKey, "100001", "基线视频"));
        metadata.Append(BuildMediaJson(PlayedKey, "200002", "正在看的视频"));
        metadata.Append(BuildMediaJson(PrebufferedNextKey, "200003", "预缓冲的下一条"));
        for (int i = 0; i < 20; i++)
        {
            string preloadKey = $"9999999999999999999999999999{i:D4}pre";
            metadata.Append(BuildMediaJson(preloadKey, (300000 + i).ToString(CultureInfo.InvariantCulture), $"预加载{i}"));
        }

        // Prebuffered next video already has a player mapping line, and baseline already played before start
        string initialWeixin =
            BuildPlayStartEvent(BaselineFeedId) +
            BuildFeedMappingLine(BaselineFeedId, BaselineKey) +
            BuildFeedMappingLine(PrebufferedNextFeedId, PrebufferedNextKey);

        reader.SetMemory(100, Encoding.UTF8.GetBytes(initialWeixin));
        reader.SetMemory(200, Encoding.UTF8.GetBytes(metadata.ToString()));

        using var capture = new MemoryWeChatVideoCapture(locator, reader, TimeProvider.System, Logger.None, _fastOptions);
        var captured = new ConcurrentQueue<WeChatVideoCaptureItem>();
        var signal = new TaskCompletionSource<WeChatVideoCaptureItem>(TaskCreationOptions.RunContinuationsAsynchronously);
        capture.ItemCaptured += (_, item) =>
        {
            captured.Enqueue(item);
            signal.TrySetResult(item);
        };

        await capture.StartAsync();
        try
        {
            Assert.Equal(WeChatVideoCaptureStatus.Running, capture.Status);

            // Phase 1 indexes metadata and pre-buffered next video exists, yet nothing is emitted
            await Task.Delay(80);
            Assert.Empty(captured);

            // Now user actually plays PlayedFeedId (videoPlayStart fires)
            reader.SetMemory(
                100,
                Encoding.UTF8.GetBytes(
                    initialWeixin +
                    BuildFeedMappingLine(PlayedFeedId, PlayedKey) +
                    BuildPlayStartEvent(PlayedFeedId)));

            WeChatVideoCaptureItem item = await signal.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("200002", item.DecodeKey);
            Assert.Equal("正在看的视频", item.Description);
            Assert.Single(captured);
        }
        finally
        {
            await capture.StopAsync();
        }

        Assert.Equal(WeChatVideoCaptureStatus.Stopped, capture.Status);
    }

    [Fact]
    public async Task Capture_GivenCacheMissOnFirstScan_ResolvesViaTargetedRetry()
    {
        var locator = new FakeProcessLocator(
        [
            new WeChatProcessInfo(100, "Weixin", IsNetworkService: false),
            new WeChatProcessInfo(200, "WeChatAppEx", IsNetworkService: false),
        ]);
        var reader = new FakeMemoryReader();
        reader.SetMemory(100, []);
        reader.SetMemory(200, []);

        var signal = new TaskCompletionSource<WeChatVideoCaptureItem>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var slowPhase1Capture = new MemoryWeChatVideoCapture(
            locator,
            reader,
            TimeProvider.System,
            Logger.None,
            new MemoryWeChatVideoCaptureOptions(
                MetadataPollInterval: TimeSpan.FromMinutes(10),
                PlaybackPollInterval: TimeSpan.FromMilliseconds(15),
                TargetedRetryDelay: TimeSpan.FromMilliseconds(60)));
        slowPhase1Capture.ItemCaptured += (_, item) => signal.TrySetResult(item);

        await slowPhase1Capture.StartAsync();
        try
        {
            // videoPlayStart + mapping appear before metadata JSON arrives in renderer memory
            reader.SetMemory(
                100,
                Encoding.UTF8.GetBytes(
                    BuildFeedMappingLine(LateFeedId, LateKey) +
                    BuildPlayStartEvent(LateFeedId)));
            await Task.Delay(20);

            reader.SetMemory(200, Encoding.UTF8.GetBytes(BuildMediaJson(LateKey, "555555", "延迟元数据")));

            WeChatVideoCaptureItem item = await signal.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("555555", item.DecodeKey);
            Assert.Equal("延迟元数据", item.Description);
        }
        finally
        {
            await slowPhase1Capture.StopAsync();
        }
    }

    [Fact]
    public async Task Capture_GivenRendererRestart_RelocatesNewPidAndCapturesVideo()
    {
        var locator = new FakeProcessLocator(
        [
            new WeChatProcessInfo(100, "Weixin", IsNetworkService: false),
            new WeChatProcessInfo(200, "WeChatAppEx", IsNetworkService: false),
        ]);
        var reader = new FakeMemoryReader();
        reader.SetMemory(100, Encoding.UTF8.GetBytes(BuildFeedMappingLine(RestartFeedId, RestartKey)));
        reader.SetMemory(200, Encoding.UTF8.GetBytes(BuildMediaJson(RestartKey, "777777", "重启后视频")));

        using var capture = new MemoryWeChatVideoCapture(locator, reader, TimeProvider.System, Logger.None, _fastOptions);
        var signal = new TaskCompletionSource<WeChatVideoCaptureItem>(TaskCreationOptions.RunContinuationsAsynchronously);
        capture.ItemCaptured += (_, item) => signal.TrySetResult(item);

        await capture.StartAsync();
        try
        {
            reader.SetAccessStatus(200, WeChatProcessAccessStatus.NotFound);
            reader.SetMemory(
                300,
                Encoding.UTF8.GetBytes(
                    BuildMediaJson(RestartKey, "777777", "重启后视频") +
                    BuildPlayStartEvent(RestartFeedId)));
            locator.SetProcesses(
            [
                new WeChatProcessInfo(100, "Weixin", IsNetworkService: false),
                new WeChatProcessInfo(300, "WeChatAppEx", IsNetworkService: false),
            ]);

            WeChatVideoCaptureItem item = await signal.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("777777", item.DecodeKey);
        }
        finally
        {
            await capture.StopAsync();
        }
    }

    [Fact]
    public async Task Capture_GivenPreviouslyWatchedBaselineVideoReplayedWithNewPlayId_CapturesReplay()
    {
        var locator = new FakeProcessLocator(
        [
            new WeChatProcessInfo(100, "Weixin", IsNetworkService: false),
            new WeChatProcessInfo(200, "WeChatAppEx", IsNetworkService: false),
        ]);
        var reader = new FakeMemoryReader();
        string initialWeixin =
            BuildPlayStartEvent(BaselineFeedId, "old-play-id-0001") +
            BuildFeedMappingLine(BaselineFeedId, BaselineKey);
        reader.SetMemory(100, Encoding.UTF8.GetBytes(initialWeixin));
        reader.SetMemory(200, Encoding.UTF8.GetBytes(BuildMediaJson(BaselineKey, "100001", "之前看过的视频")));

        using var capture = new MemoryWeChatVideoCapture(locator, reader, TimeProvider.System, Logger.None, _fastOptions);
        var signal = new TaskCompletionSource<WeChatVideoCaptureItem>(TaskCreationOptions.RunContinuationsAsynchronously);
        capture.ItemCaptured += (_, item) => signal.TrySetResult(item);

        await capture.StartAsync();
        try
        {
            await Task.Delay(50);
            Assert.False(signal.Task.IsCompleted);

            // Re-open the same video: WeChat emits videoPlayStart with a new play_id
            reader.SetMemory(
                100,
                Encoding.UTF8.GetBytes(
                    initialWeixin +
                    BuildPlayStartEvent(BaselineFeedId, "new-play-id-0002")));

            WeChatVideoCaptureItem item = await signal.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("100001", item.DecodeKey);
            Assert.Equal("之前看过的视频", item.Description);
        }
        finally
        {
            await capture.StopAsync();
        }
    }

    private static string BuildMediaJson(string encKey, string decodeKey, string description) =>
        $$"""{"nickname":"测试作者","description":"{{description}}","media":[{"url":"https://findera4.video.qq.com/251/20302/stodownload?encfilekey={{encKey}}","url_token":"&token=tok","decode_key":"{{decodeKey}}"}]}""";

    private static string BuildPlayStartEvent(string feedId, string? playId = null) => playId is null
        ? $$$"""{"action_type":"videoPlayStart","ele_info":{"feed_id":"{{{feedId}}}","loop":0}}"""
        : $$$"""{"action_type":"videoPlayStart","action_info":{"play_id":"{{{playId}}}"},"ele_info":{"feed_id":"{{{feedId}}}","loop":0}}""";

    private static string BuildFeedMappingLine(string feedId, string encKey) =>
        $"{feedId},https://findera4.video.qq.com/251/20302/stodownload?encfilekey={encKey}&hy=SH,1323848070\r\n";

    private sealed class FakeProcessLocator : IWeChatProcessLocator
    {
        private volatile IReadOnlyList<WeChatProcessInfo> _processes;

        public FakeProcessLocator(IReadOnlyList<WeChatProcessInfo>? initial = null)
        {
            _processes = initial ?? [];
        }

        public void SetProcesses(IReadOnlyList<WeChatProcessInfo> processes) => _processes = processes;

        public IReadOnlyList<WeChatProcessInfo> FindTargetProcesses() => _processes;
    }

    private sealed class FakeMemoryReader : IWeChatProcessMemoryReader
    {
        private readonly ConcurrentDictionary<int, byte[]> _memory = new();
        private readonly ConcurrentDictionary<int, WeChatProcessAccessStatus> _statuses = new();

        public void SetMemory(int processId, byte[] data)
        {
            _memory[processId] = data;
            _statuses[processId] = WeChatProcessAccessStatus.Accessible;
        }

        public void SetAccessStatus(int processId, WeChatProcessAccessStatus status) => _statuses[processId] = status;

        public WeChatProcessAccessStatus ProbeReadAccess(int processId) =>
            _statuses.GetValueOrDefault(processId, WeChatProcessAccessStatus.Accessible);

        public WeChatProcessAccessStatus ScanReadableMemory(
            int processId,
            Func<ReadOnlySpan<byte>, bool> onChunk,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WeChatProcessAccessStatus status = ProbeReadAccess(processId);
            if (status != WeChatProcessAccessStatus.Accessible)
            {
                return status;
            }

            if (_memory.TryGetValue(processId, out byte[]? data) && data.Length > 0)
            {
                onChunk(data);
            }

            return WeChatProcessAccessStatus.Accessible;
        }
    }
}
