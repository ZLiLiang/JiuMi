using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using JiuMi.Application.WeChatVideo;
using JiuMi.Infrastructure.WeChatVideo;
using Serilog.Core;

namespace JiuMi.Infrastructure.Tests;

public sealed class WeChatVideoDownloaderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "jiumi-96-dl-" + Guid.NewGuid().ToString("N"));

    public WeChatVideoDownloaderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task DownloadAsync_GivenPlainVideo_WritesTheFileAndReportsProgress()
    {
        byte[] body = CreateBody(600000);
        using var handler = new RecordingHandler(Serve(body));
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);
        var progress = new RecordingProgress();

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(Request(), progress);

        string path = Path.Combine(_directory, "clip.mp4");
        Assert.Equal(WeChatVideoDownloadStatus.Completed, result.Status);
        Assert.Equal(path, result.FilePath);
        Assert.Equal(600000, result.TotalBytes);
        Assert.Equal(body, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.part"));

        IReadOnlyList<WeChatVideoDownloadProgress> values = progress.Values;
        Assert.Equal(4, values.Count);
        Assert.Equal(new WeChatVideoDownloadProgress(0, 600000), values[0]);
        Assert.Equal(new WeChatVideoDownloadProgress(600000, 600000), values[^1]);

        RequestRecord[] requests = handler.Requests;
        Assert.Equal(HttpMethod.Head, requests[0].Method);
        Assert.Null(requests[0].Range);
        Assert.Equal("bytes=0-262143", requests[1].Range);
        Assert.Equal("bytes=262144-524287", requests[2].Range);
        Assert.Equal("bytes=524288-599999", requests[3].Range);
        Assert.All(requests, request =>
        {
            Assert.Contains("Mozilla/5.0", request.UserAgent, StringComparison.Ordinal);
            Assert.Equal("https://channels.weixin.qq.com/", request.Referer);
            Assert.Equal("https://channels.weixin.qq.com", request.Origin);
        });
    }

    [Fact]
    public async Task DownloadAsync_GivenEncryptedVideo_XorsOnlyTheReportedHead()
    {
        const ulong seed = 42;
        const int enclen = 131072;
        byte[] plain = CreateBody(300000);
        byte[] body = Encrypt(plain, seed, enclen);
        using var handler = new RecordingHandler(Serve(body, encrypted: true, enclen: enclen));
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(
            Request(decodeKey: seed.ToString(CultureInfo.InvariantCulture)));

        Assert.Equal(WeChatVideoDownloadStatus.Completed, result.Status);
        Assert.Equal(plain, await File.ReadAllBytesAsync(Path.Combine(_directory, "clip.mp4")));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task DownloadAsync_GivenExpiredHead_ReturnsExpiredWithoutRetry(HttpStatusCode status)
    {
        using var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(Request());

        Assert.Equal(WeChatVideoDownloadStatus.Expired, result.Status);
        Assert.Single(handler.Requests);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task DownloadAsync_GivenTransientChunkFailures_RetriesTwiceThenCompletes()
    {
        byte[] body = CreateBody(600000);
        int attempts = 0;
        using var handler = new RecordingHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(HeadResponse(body.Length));
            }

            RangeItemHeaderValue range = request.Headers.Range!.Ranges.Single();
            if (range.From == 0 && Interlocked.Increment(ref attempts) <= 2)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(ServeRange(body, range));
        });
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(Request());

        Assert.Equal(WeChatVideoDownloadStatus.Completed, result.Status);
        Assert.Equal(3, attempts);
        Assert.Equal(body, await File.ReadAllBytesAsync(Path.Combine(_directory, "clip.mp4")));
    }

    [Fact]
    public async Task DownloadAsync_GivenAllChunkRetriesFail_DeletesThePartialFile()
    {
        byte[] body = CreateBody(600000);
        int attempts = 0;
        using var handler = new RecordingHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(HeadResponse(body.Length));
            }

            Interlocked.Increment(ref attempts);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(Request());

        Assert.Equal(WeChatVideoDownloadStatus.Failed, result.Status);
        Assert.Equal(WeChatVideoDownloadFailure.Network, result.Failure);
        Assert.Equal(3, attempts);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task DownloadAsync_GivenContentRangeTotalMismatch_ReturnsExpired()
    {
        byte[] body = CreateBody(300000);
        using var handler = new RecordingHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(HeadResponse(body.Length));
            }

            RangeItemHeaderValue range = request.Headers.Range!.Ranges.Single();
            return Task.FromResult(ServeRange(body, range, total: body.Length + 1));
        });
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(Request());

        Assert.Equal(WeChatVideoDownloadStatus.Expired, result.Status);
        Assert.Equal(2, handler.Requests.Length);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task DownloadAsync_GivenMissingDirectory_ReturnsDirectoryMissingWithoutRequests()
    {
        using var handler = new RecordingHandler((_, _) =>
            throw new InvalidOperationException("The download must not start."));
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);
        string missing = Path.Combine(_directory, "missing");

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(Request(directory: missing));

        Assert.Equal(WeChatVideoDownloadStatus.Failed, result.Status);
        Assert.Equal(WeChatVideoDownloadFailure.DirectoryMissing, result.Failure);
        Assert.Empty(handler.Requests);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public async Task DownloadAsync_GivenExistingName_IncrementsWithoutOverwriting()
    {
        byte[] body = CreateBody(600000);
        using var handler = new RecordingHandler(Serve(body));
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);
        string existing = Path.Combine(_directory, "clip.mp4");
        await File.WriteAllTextAsync(existing, "existing");

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(Request());

        Assert.Equal(Path.Combine(_directory, "clip-1.mp4"), result.FilePath);
        Assert.Equal("existing", await File.ReadAllTextAsync(existing));
        Assert.Equal(body, await File.ReadAllBytesAsync(Path.Combine(_directory, "clip-1.mp4")));
    }

    [Fact]
    public async Task DownloadAsync_GivenCancellation_DeletesThePartialFile()
    {
        byte[] body = CreateBody(600000);
        using var handler = new RecordingHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return HeadResponse(body.Length);
            }

            RangeItemHeaderValue range = request.Headers.Range!.Ranges.Single();
            if (range.From == 0)
            {
                return ServeRange(body, range);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The request should have been canceled.");
        });
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);
        var progress = new RecordingProgress();
        using var source = new CancellationTokenSource();

        Task<WeChatVideoDownloadResult> task = downloader.DownloadAsync(Request(), progress, source.Token);
        await progress.SecondReport.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await source.CancelAsync();

        WeChatVideoDownloadResult result = await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(WeChatVideoDownloadStatus.Canceled, result.Status);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task DownloadAsync_GivenEncryptedVideoWithoutNumericKey_ReturnsProtocolFailure()
    {
        byte[] plain = CreateBody(300000);
        byte[] body = Encrypt(plain, 42, 131072);
        using var handler = new RecordingHandler(Serve(body, encrypted: true));
        using var downloader = new WeChatVideoDownloader(Logger.None, handler);

        WeChatVideoDownloadResult result = await downloader.DownloadAsync(
            Request(decodeKey: "not-a-number"));

        Assert.Equal(WeChatVideoDownloadStatus.Failed, result.Status);
        Assert.Equal(WeChatVideoDownloadFailure.Protocol, result.Failure);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    private WeChatVideoDownloadRequest Request(
        string stem = "clip",
        string? decodeKey = null,
        string? directory = null) => new()
        {
            MediaUrl = new Uri("https://finder.video.qq.com/251/20302/stodownload?token=abc"),
            OutputDirectory = directory ?? _directory,
            FileStem = stem,
            DecodeKey = decodeKey,
        };

    private static byte[] CreateBody(int length)
    {
        byte[] body = new byte[length];
        for (int index = 0; index < body.Length; index++)
        {
            body[index] = (byte)((index * 31 + 7) % 251);
        }

        return body;
    }

    private static byte[] Encrypt(byte[] plain, ulong seed, int enclen)
    {
        byte[] key = new Isaac64KeyStream(seed).CreateDecryptionArray(enclen);
        byte[] encrypted = plain.ToArray();
        for (int index = 0; index < Math.Min(enclen, encrypted.Length); index++)
        {
            encrypted[index] ^= key[index];
        }

        return encrypted;
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Serve(
        byte[] body,
        bool encrypted = false,
        int enclen = 131072) => (request, _) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(HeadResponse(body.Length, encrypted, enclen));
            }

            RangeItemHeaderValue range = request.Headers.Range!.Ranges.Single();
            return Task.FromResult(ServeRange(body, range, encrypted: encrypted, enclen: enclen));
        };

    private static HttpResponseMessage HeadResponse(
        long length,
        bool encrypted = false,
        int enclen = 131072)
    {
        var content = new ByteArrayContent([]);
        content.Headers.ContentLength = length;
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        AddEncryption(response, encrypted, enclen);
        return response;
    }

    private static HttpResponseMessage ServeRange(
        byte[] body,
        RangeItemHeaderValue range,
        long? total = null,
        bool encrypted = false,
        int enclen = 131072)
    {
        long from = range.From!.Value;
        long to = range.To!.Value;
        byte[] chunk = body.AsSpan((int)from, (int)(to - from + 1)).ToArray();
        return RangeResponse(from, to, total ?? body.Length, chunk, encrypted, enclen);
    }

    private static HttpResponseMessage RangeResponse(
        long from,
        long to,
        long total,
        byte[] body,
        bool encrypted = false,
        int enclen = 131072)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, total);
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content };
        AddEncryption(response, encrypted, enclen);
        return response;
    }

    private static void AddEncryption(HttpResponseMessage response, bool encrypted, int enclen)
    {
        if (!encrypted)
        {
            return;
        }

        response.Headers.TryAddWithoutValidation("X-encflag", "1");
        response.Headers.TryAddWithoutValidation(
            "X-enclen",
            enclen.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class RecordingProgress : IProgress<WeChatVideoDownloadProgress>
    {
        private readonly object _gate = new();
        private readonly List<WeChatVideoDownloadProgress> _values = [];

        public TaskCompletionSource SecondReport
        {
            get;
        } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<WeChatVideoDownloadProgress> Values
        {
            get
            {
                lock (_gate)
                {
                    return _values.ToArray();
                }
            }
        }

        public void Report(WeChatVideoDownloadProgress value)
        {
            lock (_gate)
            {
                _values.Add(value);
                if (_values.Count >= 2)
                {
                    SecondReport.TrySetResult();
                }
            }
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
        private readonly List<RequestRecord> _requests = [];

        public RecordingHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public RequestRecord[] Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToArray();
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var record = new RequestRecord(
                request.Method,
                request.RequestUri!,
                request.Headers.UserAgent.ToString(),
                request.Headers.Referrer?.AbsoluteUri,
                request.Headers.TryGetValues("Origin", out IEnumerable<string>? origins)
                    ? origins.FirstOrDefault()
                    : null,
                request.Headers.Range?.ToString());
            lock (_requests)
            {
                _requests.Add(record);
            }

            return await _responder(request, cancellationToken);
        }
    }

    private sealed record RequestRecord(
        HttpMethod Method,
        Uri Uri,
        string UserAgent,
        string? Referer,
        string? Origin,
        string? Range);
}
