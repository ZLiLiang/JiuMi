using JiuMi.Application.Pet;

namespace JiuMi.Application.Tests;

public sealed class PetAnimationCatalogTests
{
    [Theory]
    [InlineData(PetState.Idle, "idle", 12, 110, true)]
    [InlineData(PetState.Blink, "blink", 3, 80, false)]
    [InlineData(PetState.React, "react", 3, 80, false)]
    [InlineData(PetState.Drag, "drag", 2, 150, true)]
    [InlineData(PetState.SleepIn, "sleep-in", 4, 120, false)]
    [InlineData(PetState.Sleep, "sleep", 4, 300, true)]
    [InlineData(PetState.Wake, "wake", 2, 100, false)]
    [InlineData(PetState.WalkOut, "walk", 8, 100, true)]
    public void For_GivenState_DescribesTheExportedFrames(
        PetState state,
        string folder,
        int frameCount,
        int millisecondsPerFrame,
        bool loops)
    {
        PetAnimationClip clip = PetAnimationCatalog.For(state);

        Assert.Equal(folder, clip.AssetFolder);
        Assert.Equal(loops, clip.Loops);
        Assert.Equal(frameCount, clip.FrameDurations.Count);
        Assert.All(
            clip.FrameDurations,
            duration => Assert.Equal(TimeSpan.FromMilliseconds(millisecondsPerFrame), duration));
    }
}
