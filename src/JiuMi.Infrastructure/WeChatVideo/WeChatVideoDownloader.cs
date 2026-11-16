using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Downloads WeChat video media in fixed-size ranges, decrypts the reported head and renames the
/// partial file only after every byte arrived.
/// </summary>
public sealed class WeChatVideoDownloader : IWeChatVideoDownloader, IDisposable
{
    private const int ChunkSize = 262144;
    private const int RetryCount = 2;
    private const int MaxEncryptionLength = 16 * 1024 * 1024;

    private readonly HttpClient _client;
    private readonly Serilog.ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoDownloader"/> class.
    /// </summary>
    /// <param name="logger">Records chunk and file failures.</param>
    public WeChatVideoDownloader(Serilog.ILogger logger)
        : this(logger, handler: null)
    {
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "HttpClient is created with disposeHandler: true and owns the handler.")]
    internal WeChatVideoDownloader(Serilog.ILogger logger, HttpMessageHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _client = handler is null
            ? new HttpClient(new HttpClientHandler { UseProxy = false }, disposeHandler: true)
            : new HttpClient(handler, disposeHandler: true);
        _client.Timeout = TimeSpan.FromMinutes(10);
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The stream is disposed before the rename or in the finally block.")]
    public async Task<WeChatVideoDownloadResult> DownloadAsync(
        WeChatVideoDownloadRequest request,
        IProgress<WeChatVideoDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileStem);

        if (!Directory.Exists(request.OutputDirectory))
        {
            return WeChatVideoDownloadResult.Failed(WeChatVideoDownloadFailure.DirectoryMissing);
        }

        string stem = WeChatVideoFileName.ResolveUniqueStem(request.OutputDirectory, request.FileStem);
        string partPath = Path.Combine(
            request.OutputDirectory,
            stem + WeChatVideoFileName.PartExtension);
        string finalPath = Path.Combine(
            request.OutputDirectory,
            stem + WeChatVideoFileName.Extension);

        long received = 0;
        long? total = null;
        bool succeeded = false;
        bool partCreated = false;
        FileStream? output = null;

        try
        {
            using HttpResponseMessage head = await SendAsync(
                HttpMethod.Head,
                request.MediaUrl,
                range: null,
                cancellationToken).ConfigureAwait(false);
            if (IsExpired(head.StatusCode))
            {
                return WeChatVideoDownloadResult.Expired();
            }

            if (!head.IsSuccessStatusCode)
            {
                _logger.Warning(
                    "WeChat video HEAD failed with {StatusCode}",
                    (int)head.StatusCode);
                return WeChatVideoDownloadResult.Failed(WeChatVideoDownloadFailure.Network);
            }

            long length = head.Content?.Headers.ContentLength ?? -1;
            if (length <= 0)
            {
                _logger.Warning("WeChat video HEAD did not report a usable Content-Length");
                return WeChatVideoDownloadResult.Failed(WeChatVideoDownloadFailure.Protocol);
            }

            total = length;
            progress?.Report(new WeChatVideoDownloadProgress(0, length));

            output = new FileStream(
                partPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            partCreated = true;

            byte[]? keyStream = null;
            int encryptionLength = 0;
            bool encrypted = false;

            while (received < length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long from = received;
                long to = Math.Min(received + ChunkSize - 1, length - 1);
                int expected = (int)(to - from + 1);
                ChunkFetchResult fetch = await FetchChunkAsync(
                    request.MediaUrl,
                    from,
                    to,
                    expected,
                    length,
                    cancellationToken).ConfigureAwait(false);

                switch (fetch.Status)
                {
                    case ChunkFetchStatus.Expired:
                        return WeChatVideoDownloadResult.Expired(received, total);
                    case ChunkFetchStatus.InvalidHeaders:
                    case ChunkFetchStatus.ProtocolFailure:
                        _logger.Warning(
                            "WeChat video chunk at {Offset} failed the media protocol " +
                            "({ErrorType})",
                            from,
                            fetch.Error?.GetType().Name);
                        return WeChatVideoDownloadResult.Failed(
                            WeChatVideoDownloadFailure.Protocol,
                            received,
                            total,
                            fetch.Error);
                    case ChunkFetchStatus.Failed:
                        _logger.Warning(
                            "WeChat video chunk at {Offset} failed after {Attempts} attempts " +
                            "({ErrorType})",
                            from,
                            RetryCount + 1,
                            fetch.Error?.GetType().Name);
                        return WeChatVideoDownloadResult.Failed(
                            WeChatVideoDownloadFailure.Network,
                            received,
                            total,
                            fetch.Error);
                    default:
                        break;
                }

                byte[] chunk = fetch.Chunk;
                if (from == 0 && fetch.Headers.IsEncrypted)
                {
                    if (!ulong.TryParse(
                        request.DecodeKey,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out ulong seed))
                    {
                        return WeChatVideoDownloadResult.Failed(
                            WeChatVideoDownloadFailure.Protocol,
                            received,
                            total);
                    }

                    encrypted = true;
                    encryptionLength = fetch.Headers.Length;
                    int keyLength = (int)Math.Min(encryptionLength, length);
                    keyLength = (keyLength + 7) & ~7;
                    keyStream = new Isaac64KeyStream(seed).CreateDecryptionArray(keyLength);
                }

                if (encrypted && keyStream is not null)
                {
                    Decrypt(chunk, from, encryptionLength, keyStream);
                }

                await output.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
                received += chunk.Length;
                progress?.Report(new WeChatVideoDownloadProgress(received, length));
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            await output.DisposeAsync().ConfigureAwait(false);
            output = null;
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partPath, finalPath);
            succeeded = true;
            return WeChatVideoDownloadResult.Success(finalPath, received, length);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return WeChatVideoDownloadResult.Canceled(received, total);
        }
        catch (OperationCanceledException exception)
        {
            _logger.Warning(
                "WeChat video download timed out ({ErrorType})",
                exception.GetType().Name);
            return WeChatVideoDownloadResult.Failed(
                WeChatVideoDownloadFailure.Network,
                received,
                total,
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.Warning(exception, "WeChat video could not be written to the output directory");
            return WeChatVideoDownloadResult.Failed(
                WeChatVideoDownloadFailure.FileWrite,
                received,
                total,
                exception);
        }
        catch (IOException exception)
        {
            _logger.Warning(exception, "WeChat video download failed while writing the file");
            return WeChatVideoDownloadResult.Failed(
                WeChatVideoDownloadFailure.FileWrite,
                received,
                total,
                exception);
        }
        finally
        {
            if (output is not null)
            {
                await DisposeQuietlyAsync(output).ConfigureAwait(false);
            }

            if (!succeeded && partCreated)
            {
                TryDelete(partPath);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private async Task<ChunkFetchResult> FetchChunkAsync(
        Uri mediaUrl,
        long from,
        long to,
        int expected,
        long total,
        CancellationToken cancellationToken)
    {
        EncryptionHeaders headers = default;
        Exception? lastError = null;
        for (int attempt = 0; attempt <= RetryCount; attempt++)
        {
            try
            {
                using HttpResponseMessage response = await SendAsync(
                    HttpMethod.Get,
                    mediaUrl,
                    (from, to),
                    cancellationToken).ConfigureAwait(false);
                if (IsExpired(response.StatusCode))
                {
                    return ChunkFetchResult.Expired();
                }

                if (!response.IsSuccessStatusCode)
                {
                    lastError = new HttpRequestException(
                        "The media server returned " +
                        ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) +
                        ".");
                    continue;
                }

                ContentRangeHeaderValue? range = response.Content.Headers.ContentRange;
                if (response.StatusCode != HttpStatusCode.PartialContent || range is null)
                {
                    return ChunkFetchResult.ProtocolFailure(
                        new HttpRequestException(
                            "The media server did not return the requested range."));
                }

                if (!IsExpectedRange(range, from, to, total))
                {
                    return ChunkFetchResult.Expired();
                }

                if (from == 0)
                {
                    headers = ReadEncryptionHeaders(response);
                    if (headers.IsInvalid)
                    {
                        return ChunkFetchResult.InvalidHeaders(headers);
                    }
                }

                byte[]? chunk = await ReadExactlyAsync(
                    response.Content,
                    expected,
                    cancellationToken).ConfigureAwait(false);
                if (chunk is not null)
                {
                    return ChunkFetchResult.Success(chunk, headers);
                }

                lastError = new IOException("The media server returned fewer bytes than requested.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException exception)
            {
                lastError = exception;
            }
            catch (IOException exception)
            {
                lastError = exception;
            }
        }

        return ChunkFetchResult.Failed(headers, lastError);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        Uri mediaUrl,
        (long From, long To)? range,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, mediaUrl);
        WeChatMediaRequest.Apply(request, "*/*");
        if (range is { } bounds)
        {
            request.Headers.Range = new RangeHeaderValue(bounds.From, bounds.To);
        }

        return await _client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadExactlyAsync(
        HttpContent content,
        int length,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[length];
        await using Stream stream = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream
                .ReadAsync(buffer.AsMemory(offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            offset += read;
        }

        return buffer;
    }

    private static bool IsExpectedRange(
        ContentRangeHeaderValue range,
        long from,
        long to,
        long total)
    {
        return range.From == from &&
            range.To == to &&
            range.Length == total;
    }

    private static EncryptionHeaders ReadEncryptionHeaders(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-encflag", out IEnumerable<string>? flags) ||
            !string.Equals(
                flags.FirstOrDefault()?.Trim(),
                "1",
                StringComparison.Ordinal))
        {
            return new EncryptionHeaders(IsEncrypted: false, IsInvalid: false, Length: 0);
        }

        if (!response.Headers.TryGetValues("X-enclen", out IEnumerable<string>? lengths))
        {
            return new EncryptionHeaders(IsEncrypted: true, IsInvalid: true, Length: 0);
        }

        if (!int.TryParse(
                lengths.FirstOrDefault()?.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int length) ||
            length <= 0 ||
            length > MaxEncryptionLength)
        {
            return new EncryptionHeaders(IsEncrypted: true, IsInvalid: true, Length: 0);
        }

        return new EncryptionHeaders(IsEncrypted: true, IsInvalid: false, length);
    }

    private static void Decrypt(
        byte[] chunk,
        long offset,
        int encryptionLength,
        byte[] keyStream)
    {
        long limit = Math.Min(encryptionLength, keyStream.Length);
        long end = Math.Min(offset + chunk.Length, limit);
        for (long index = offset; index < end; index++)
        {
            chunk[(int)(index - offset)] ^= keyStream[(int)index];
        }
    }

    private static bool IsExpired(HttpStatusCode status)
    {
        return status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Gone;
    }

    private async Task DisposeQuietlyAsync(FileStream stream)
    {
        try
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            _logger.Warning(exception, "WeChat video could not dispose the partial file stream");
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.Warning(exception, "WeChat video could not dispose the partial file stream");
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException exception)
        {
            _logger.Warning(exception, "WeChat video could not delete the partial file");
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.Warning(exception, "WeChat video could not delete the partial file");
        }
    }

    private readonly record struct EncryptionHeaders(bool IsEncrypted, bool IsInvalid, int Length);

    private enum ChunkFetchStatus
    {
        Success,
        Expired,
        InvalidHeaders,
        ProtocolFailure,
        Failed,
    }

    private readonly record struct ChunkFetchResult(
        ChunkFetchStatus Status,
        byte[] Chunk,
        EncryptionHeaders Headers,
        Exception? Error)
    {
        public static ChunkFetchResult Success(byte[] chunk, EncryptionHeaders headers) =>
            new(ChunkFetchStatus.Success, chunk, headers, Error: null);

        public static ChunkFetchResult Expired() =>
            new(ChunkFetchStatus.Expired, [], default, Error: null);

        public static ChunkFetchResult InvalidHeaders(EncryptionHeaders headers) =>
            new(ChunkFetchStatus.InvalidHeaders, [], headers, Error: null);

        public static ChunkFetchResult ProtocolFailure(Exception? error = null) =>
            new(ChunkFetchStatus.ProtocolFailure, [], default, error);

        public static ChunkFetchResult Failed(EncryptionHeaders headers, Exception? error) =>
            new(ChunkFetchStatus.Failed, [], headers, error);
    }
}
