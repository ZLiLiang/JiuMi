using System.ComponentModel;
using System.Globalization;
using JiuMi.Application.Localization;
using JiuMi.Application.Notifications;
using JiuMi.Application.Updates;
using Serilog;

namespace JiuMi.Application.Tests;

public sealed class AppUpdateSchedulerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunOnceAsync_GivenShouldNotifyUpdate_ShowsBalloonAndOpensReleasePageOnClick()
    {
        Uri pageUrl = new("https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.5.0");
        AppUpdateCandidate candidate = new(
            new AppUpdateRelease(
                SemanticVersion.Parse("1.5.0"),
                IsPrerelease: false,
                _now,
                pageUrl,
                AssetUrl: null,
                AssetSha256: null,
                AssetSize: 0),
            new AppUpdateSource("Gitee", "https://gitee.com/ZLiLiang/JiuMi", pageUrl));

        var checker = new StubChecker(new AppUpdateCheckResult(
            AppUpdateCheckStatus.UpdateAvailable,
            candidate,
            ShouldNotify: true,
            LastCheckedAt: _now));
        var notifications = new StubNotification();
        var launcher = new StubLauncher();
        using var scheduler = new AppUpdateScheduler(
            checker,
            notifications,
            new StubApplicationInfo(),
            launcher,
            new FixedTimeProvider(_now),
            new LoggerConfiguration().CreateLogger());

        TimeSpan nextDelay = await scheduler.RunOnceAsync(new StubLocalization(), action => action());

        Assert.Equal(AppUpdateCheckPolicy.Interval, nextDelay);
        Assert.Equal(1, notifications.ShowCount);
        Assert.Equal("JiuMi", notifications.LastTitle);
        Assert.Contains("1.5.0", notifications.LastMessage, StringComparison.Ordinal);
        Assert.NotNull(notifications.LastActivated);

        notifications.LastActivated!();
        Assert.Equal(pageUrl, launcher.OpenedUri);
    }

    [Fact]
    public async Task RunOnceAsync_GivenAlreadyNotifiedUpdate_DoesNotShowBalloon()
    {
        Uri pageUrl = new("https://gitee.com/ZLiLiang/JiuMi/releases/tag/v1.5.0");
        AppUpdateCandidate candidate = new(
            new AppUpdateRelease(
                SemanticVersion.Parse("1.5.0"),
                IsPrerelease: false,
                _now,
                pageUrl,
                AssetUrl: null,
                AssetSha256: null,
                AssetSize: 0),
            new AppUpdateSource("Gitee", "https://gitee.com/ZLiLiang/JiuMi", pageUrl));

        var checker = new StubChecker(new AppUpdateCheckResult(
            AppUpdateCheckStatus.UpdateAvailable,
            candidate,
            ShouldNotify: false,
            LastCheckedAt: _now));
        var notifications = new StubNotification();
        var launcher = new StubLauncher();
        using var scheduler = new AppUpdateScheduler(
            checker,
            notifications,
            new StubApplicationInfo(),
            launcher,
            new FixedTimeProvider(_now),
            new LoggerConfiguration().CreateLogger());

        await scheduler.RunOnceAsync(new StubLocalization(), action => action());

        Assert.Equal(0, notifications.ShowCount);
    }

    [Fact]
    public async Task RunOnceAsync_GivenSkippedBecauseRecentlyChecked_SchedulesRemainingDelay()
    {
        DateTimeOffset checkedSixHoursAgo = _now.AddHours(-6);
        var checker = new StubChecker(new AppUpdateCheckResult(
            AppUpdateCheckStatus.Skipped,
            AvailableUpdate: null,
            ShouldNotify: false,
            LastCheckedAt: checkedSixHoursAgo));
        using var scheduler = new AppUpdateScheduler(
            checker,
            new StubNotification(),
            new StubApplicationInfo(),
            new StubLauncher(),
            new FixedTimeProvider(_now),
            new LoggerConfiguration().CreateLogger());

        TimeSpan nextDelay = await scheduler.RunOnceAsync(new StubLocalization(), action => action());

        Assert.Equal(TimeSpan.FromHours(18), nextDelay);
    }

    private sealed class StubChecker(AppUpdateCheckResult result) : IAppUpdateChecker
    {
        public AppUpdateStatus Status { get; } = new(AppUpdateCheckState.Idle, result.LastCheckedAt, result.AvailableUpdate);

        public event EventHandler? StatusChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public Task<AppUpdateCheckResult> CheckAsync(bool isManual, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class StubNotification : IUserNotification
    {
        public int ShowCount
        {
            get; private set;
        }

        public string? LastTitle
        {
            get; private set;
        }

        public string? LastMessage
        {
            get; private set;
        }

        public Action? LastActivated
        {
            get; private set;
        }

        public void Show(string title, string message, Action? activated)
        {
            ShowCount++;
            LastTitle = title;
            LastMessage = message;
            LastActivated = activated;
        }
    }

    private sealed class StubLauncher : ISystemLauncher
    {
        public Uri? OpenedUri
        {
            get; private set;
        }

        public void OpenFolder(string path)
        {
        }

        public void OpenUri(Uri uri) => OpenedUri = uri;
    }

    private sealed class StubApplicationInfo : IApplicationInfo
    {
        public string Name => "JiuMi";

        public string Version => "1.4.1";

        public string RepositoryUrl => "https://github.com/ZLiLiang/JiuMi";

        public string GiteeRepositoryUrl => "https://gitee.com/ZLiLiang/JiuMi";

        public IReadOnlyList<AppUpdateSource> UpdateSources => [];
    }

    private sealed class StubLocalization : ILocalizationService
    {
        public string CurrentLanguage => "en-US";

        public CultureInfo Culture => CultureInfo.InvariantCulture;

        public IReadOnlyList<LanguageOption> Languages => [];

        public string this[string key] => key;

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public string Format(string key, params object?[] args) =>
            string.Format(CultureInfo.InvariantCulture, "{0}:{1}", key, string.Join(',', args));

        public bool TrySetLanguage(string languageCode) => true;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
