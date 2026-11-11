using JiuMi.Application.MyJiuMi;

namespace JiuMi.Application.Tests;

/// <summary>
/// The frame plan frozen by the art spec: every state maps to one asset folder, frame count and timing.
/// </summary>
public sealed class MyJiuMiAnimationCatalogTests
{
    [Theory]
    [InlineData(MyJiuMiState.LyingIdle, "lying-idle", 2, 1500, true)]
    [InlineData(MyJiuMiState.Blink, "blink", 3, 80, false)]
    [InlineData(MyJiuMiState.React, "react", 2, 120, false)]
    [InlineData(MyJiuMiState.Drag, "drag", 2, 400, true)]
    [InlineData(MyJiuMiState.SleepIn, "sleep-in", 3, 150, false)]
    [InlineData(MyJiuMiState.Sleep, "sleep", 2, 2000, true)]
    [InlineData(MyJiuMiState.Wake, "wake", 3, 150, false)]
    [InlineData(MyJiuMiState.Stretch, "stretch", 4, 120, false)]
    [InlineData(MyJiuMiState.Walk, "walk", 33, 26, true)]
    public void For_GivenState_MatchesTheFrozenFramePlan(
        MyJiuMiState state,
        string folder,
        int frameCount,
        int frameMilliseconds,
        bool loops)
    {
        MyJiuMiAnimationClip clip = MyJiuMiAnimationCatalog.GetClip(state);

        Assert.Equal(folder, clip.AssetFolder);
        Assert.Equal(frameCount, clip.FrameDurations.Count);
        Assert.All(
            clip.FrameDurations,
            duration => Assert.Equal(TimeSpan.FromMilliseconds(frameMilliseconds), duration));
        Assert.Equal(loops, clip.Loops);
    }

    [Fact]
    public void Duration_GivenEveryState_IsTheSumOfItsFrames()
    {
        foreach (MyJiuMiState state in Enum.GetValues<MyJiuMiState>())
        {
            MyJiuMiAnimationClip clip = MyJiuMiAnimationCatalog.GetClip(state);
            TimeSpan expected = TimeSpan.FromTicks(
                clip.FrameDurations.Sum(duration => duration.Ticks));

            Assert.Equal(expected, clip.Duration);
        }
    }

    [Fact]
    public void Duration_GivenWalk_IsTheReferenceCycle()
    {
        Assert.Equal(
            TimeSpan.FromMilliseconds(858),
            MyJiuMiAnimationCatalog.GetClip(MyJiuMiState.Walk).Duration);
    }
}
