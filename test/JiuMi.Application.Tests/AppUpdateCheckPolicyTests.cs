using JiuMi.Application.Updates;

namespace JiuMi.Application.Tests;

public sealed class AppUpdateCheckPolicyTests
{
    private static readonly DateTimeOffset _baseTime = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsDue_GivenNeverChecked_ReturnsTrue()
    {
        Assert.True(AppUpdateCheckPolicy.IsDue(lastCheckedAt: null, _baseTime));
    }

    [Fact]
    public void IsDue_GivenWithin24Hours_ReturnsFalse()
    {
        DateTimeOffset now = _baseTime.AddHours(23).AddMinutes(59);

        Assert.False(AppUpdateCheckPolicy.IsDue(_baseTime, now));
    }

    [Fact]
    public void IsDue_Given24HoursElapsed_ReturnsTrue()
    {
        DateTimeOffset now = _baseTime.AddHours(24);

        Assert.True(AppUpdateCheckPolicy.IsDue(_baseTime, now));
    }

    [Fact]
    public void GetNextDelay_GivenRecentCheck_ReturnsRemainingTimeUntil24Hours()
    {
        DateTimeOffset now = _baseTime.AddHours(6);

        TimeSpan delay = AppUpdateCheckPolicy.GetNextDelay(_baseTime, now);

        Assert.Equal(TimeSpan.FromHours(18), delay);
    }

    [Fact]
    public void GetNextDelay_GivenDueOrNeverChecked_ReturnsFullInterval()
    {
        Assert.Equal(AppUpdateCheckPolicy.Interval, AppUpdateCheckPolicy.GetNextDelay(null, _baseTime));
        Assert.Equal(AppUpdateCheckPolicy.Interval, AppUpdateCheckPolicy.GetNextDelay(_baseTime, _baseTime.AddHours(25)));
    }

    [Fact]
    public void GetNextDelay_GivenClockMovedBackwards_ClampsToInterval()
    {
        DateTimeOffset futureCheck = _baseTime.AddHours(5);

        TimeSpan delay = AppUpdateCheckPolicy.GetNextDelay(futureCheck, _baseTime);

        Assert.Equal(AppUpdateCheckPolicy.Interval, delay);
    }
}
