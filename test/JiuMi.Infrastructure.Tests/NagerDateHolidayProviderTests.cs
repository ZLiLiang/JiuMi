using System.Net;
using System.Net.Http;
using JiuMi.Application.Settings;
using JiuMi.Application.WorldClock;
using JiuMi.Infrastructure.WorldClock;
using Serilog;

namespace JiuMi.Infrastructure.Tests;

public sealed class NagerDateHolidayProviderTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "JiuMi.Tests." + Guid.NewGuid().ToString("N"));
    private readonly TestSettingsStore _settingsStore;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();
    private readonly MutableTimeProvider _timeProvider = new();

    public NagerDateHolidayProviderTests()
    {
        Directory.CreateDirectory(_root);
        _settingsStore = new TestSettingsStore(
            SettingsLocation.ForDirectory(SettingsLocationKind.ExecutableDirectory, _root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task GetHolidaysAsync_GivenSuccessfulResponse_WritesCacheAndReusesIt()
    {
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """[{"date":"2026-01-01","localName":"New Year"},{"date":"2026-12-25"}]"""));
        using (var provider = CreateProvider(handler))
        {
            HolidayLookup first = await provider.GetHolidaysAsync("US", 2026, allowNetwork: true);

            Assert.Equal(HolidayLookupStatus.Available, first.Status);
            Assert.Equal(2, first.Dates.Count);
            Assert.Equal(1, handler.CallCount);
        }

        string cachePath = Path.Combine(_root, "cache", "holidays", "US-2026.json");
        Assert.True(File.Exists(cachePath));

        using var offlineHandler = new StubHandler(_ => throw new InvalidOperationException("no network"));
        using var second = CreateProvider(offlineHandler);
        HolidayLookup cached = await second.GetHolidaysAsync("US", 2026, allowNetwork: true);

        Assert.Equal(HolidayLookupStatus.Available, cached.Status);
        Assert.Equal(2, cached.Dates.Count);
        Assert.Equal(0, offlineHandler.CallCount);
    }

    [Fact]
    public async Task GetHolidaysAsync_GivenNotFound_ReturnsNoDataWithoutRetry()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var provider = CreateProvider(handler);

        HolidayLookup lookup = await provider.GetHolidaysAsync("US", 2026, allowNetwork: true);

        Assert.Equal(HolidayLookupStatus.NoData, lookup.Status);
        Assert.Empty(lookup.Dates);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetHolidaysAsync_GivenEmptyArray_ReturnsNoData()
    {
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));
        using var provider = CreateProvider(handler);

        HolidayLookup lookup = await provider.GetHolidaysAsync("US", 2026, allowNetwork: true);

        Assert.Equal(HolidayLookupStatus.NoData, lookup.Status);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetHolidaysAsync_GivenServerError_RetriesOnceThenFails()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var provider = CreateProvider(handler);

        HolidayLookup lookup = await provider.GetHolidaysAsync("US", 2026, allowNetwork: true);

        Assert.Equal(HolidayLookupStatus.Failed, lookup.Status);
        Assert.NotNull(lookup.Error);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetHolidaysAsync_GivenNetworkDisabled_ReadsTheCacheWithoutRequests()
    {
        using (var onlineHandler = new StubHandler(_ => Json(HttpStatusCode.OK, """[{"date":"2026-01-01"}]""")))
        using (var online = CreateProvider(onlineHandler))
        {
            await online.GetHolidaysAsync("US", 2026, allowNetwork: true);
        }

        using var handler = new StubHandler(_ => throw new InvalidOperationException("no network"));
        using var provider = CreateProvider(handler);
        HolidayLookup lookup = await provider.GetHolidaysAsync("US", 2026, allowNetwork: false);

        Assert.Equal(HolidayLookupStatus.Available, lookup.Status);
        Assert.Single(lookup.Dates);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetHolidaysAsync_GivenNetworkDisabledAndNoCache_ReturnsDisabled()
    {
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));
        using var provider = CreateProvider(handler);

        HolidayLookup lookup = await provider.GetHolidaysAsync("US", 2026, allowNetwork: false);

        Assert.Equal(HolidayLookupStatus.Disabled, lookup.Status);
        Assert.Empty(lookup.Dates);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetHolidaysAsync_GivenExpiredCacheAndFailedRequest_ReturnsTheStaleCache()
    {
        using (var onlineHandler = new StubHandler(_ => Json(HttpStatusCode.OK, """[{"date":"2026-01-01"}]""")))
        using (var online = CreateProvider(onlineHandler))
        {
            await online.GetHolidaysAsync("US", 2026, allowNetwork: true);
        }

        _timeProvider.Advance(TimeSpan.FromDays(8));

        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var provider = CreateProvider(handler);
        HolidayLookup lookup = await provider.GetHolidaysAsync("US", 2026, allowNetwork: true);

        Assert.Equal(HolidayLookupStatus.Available, lookup.Status);
        Assert.Single(lookup.Dates);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetHolidaysAsync_GivenCancellation_DoesNotRetry()
    {
        using var handler = new StubHandler(_ => throw new OperationCanceledException());
        using var provider = CreateProvider(handler);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetHolidaysAsync("US", 2026, allowNetwork: true, source.Token));
        Assert.True(handler.CallCount <= 1);
    }

    private NagerDateHolidayProvider CreateProvider(HttpMessageHandler handler) =>
        new(_settingsStore, _timeProvider, _logger, handler);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body),
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public int CallCount
        {
            get;
            private set;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class TestSettingsStore : ISettingsStore
    {
        public TestSettingsStore(SettingsLocation location)
        {
            Location = location;
        }

        public SettingsLocation Location
        {
            get;
        }

        public AppSettings Current => AppSettings.CreateDefault();

        public SettingsLoadResult Load() => new(Current, Location, SettingsLoadIssue.None);

        public SettingsSaveResult Save(AppSettings settings) => SettingsSaveResult.NotPersisted;
    }
}
