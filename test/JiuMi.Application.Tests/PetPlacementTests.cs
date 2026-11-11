using JiuMi.Application.Pet;

namespace JiuMi.Application.Tests;

public sealed class PetPlacementTests
{
    private static readonly PetWorkArea _primary = new(0, 0, 1920, 1040, 1d, IsPrimary: true);

    [Fact]
    public void GetDefault_GivenPrimaryScreen_PlacesAtBottomRightWithMargin()
    {
        PetPlacementResult result = PetPlacement.GetDefault([_primary]);

        Assert.Equal(new PetPoint(1736, 856), result.Position);
        Assert.Equal(160, result.PixelSize);
        Assert.Equal(_primary, result.WorkArea);
    }

    [Fact]
    public void GetDefault_GivenScaling200Percent_ScalesSizeAndMargin()
    {
        var scaled = new PetWorkArea(0, 0, 3840, 2080, 2d, IsPrimary: true);

        PetPlacementResult result = PetPlacement.GetDefault([scaled]);

        Assert.Equal(new PetPoint(3472, 1712), result.Position);
        Assert.Equal(320, result.PixelSize);
    }

    [Fact]
    public void GetDefault_GivenPrimaryNotFirst_UsesThePrimaryScreen()
    {
        var secondary = new PetWorkArea(1920, 0, 1280, 1024, 1d, IsPrimary: false);

        PetPlacementResult result = PetPlacement.GetDefault([secondary, _primary]);

        Assert.Equal(_primary, result.WorkArea);
        Assert.Equal(new PetPoint(1736, 856), result.Position);
    }

    [Fact]
    public void GetDefault_GivenNoPrimaryScreen_UsesTheFirstScreen()
    {
        var first = new PetWorkArea(1920, 0, 1280, 1024, 1d, IsPrimary: false);
        var second = new PetWorkArea(3200, 0, 1280, 1024, 1d, IsPrimary: false);

        PetPlacementResult result = PetPlacement.GetDefault([first, second]);

        Assert.Equal(first, result.WorkArea);
        Assert.Equal(new PetPoint(3016, 840), result.Position);
    }

    [Fact]
    public void Resolve_GivenPositionInsideSecondaryScreen_KeepsIt()
    {
        var secondary = new PetWorkArea(1920, 0, 1280, 1024, 1d, IsPrimary: false);

        PetPlacementResult result = PetPlacement.Resolve(new PetPoint(2200, 300), [secondary, _primary]);

        Assert.Equal(new PetPoint(2200, 300), result.Position);
        Assert.Equal(secondary, result.WorkArea);
    }

    [Fact]
    public void Resolve_GivenPositionOffTheScreen_ClampsInsideTheNearestScreen()
    {
        var secondary = new PetWorkArea(1920, 0, 1280, 1024, 1d, IsPrimary: false);

        PetPlacementResult result = PetPlacement.Resolve(new PetPoint(3300, 1500), [secondary, _primary]);

        Assert.Equal(new PetPoint(3040, 864), result.Position);
        Assert.Equal(secondary, result.WorkArea);
    }

    [Fact]
    public void Resolve_GivenNoMatchingScreen_ClampsIntoTheClosestScreen()
    {
        PetPlacementResult result = PetPlacement.Resolve(new PetPoint(-500, -500), [_primary]);

        Assert.Equal(new PetPoint(0, 0), result.Position);
        Assert.Equal(_primary, result.WorkArea);
    }

    [Fact]
    public void Resolve_GivenScreenSmallerThanPet_AlignsToScreenOrigin()
    {
        var tiny = new PetWorkArea(10, 20, 100, 100, 1d, IsPrimary: true);

        PetPlacementResult result = PetPlacement.Resolve(new PetPoint(50, 50), [tiny]);

        Assert.Equal(new PetPoint(10, 20), result.Position);
    }

    [Fact]
    public void CreateEntrancePath_GivenDistanceAtFullScale_TakesVelocityDuration()
    {
        PetEntrancePath path = PetPlacement.CreateEntrancePath(
            new PetPoint(0, 0),
            new PetPoint(450, 0),
            scaling: 1d);

        Assert.InRange(path.Duration.TotalSeconds, 0.999d, 1.001d);
    }

    [Fact]
    public void CreateEntrancePath_GivenScaling_MeasuresSpeedInDeviceIndependentPixels()
    {
        PetEntrancePath path = PetPlacement.CreateEntrancePath(
            new PetPoint(0, 0),
            new PetPoint(900, 0),
            scaling: 2d);

        Assert.InRange(path.Duration.TotalSeconds, 0.999d, 1.001d);
    }

    [Fact]
    public void CreateEntrancePath_GivenSamePoint_HasZeroDuration()
    {
        PetEntrancePath path = PetPlacement.CreateEntrancePath(
            new PetPoint(100, 100),
            new PetPoint(100, 100),
            scaling: 1d);

        Assert.Equal(TimeSpan.Zero, path.Duration);
    }

    [Fact]
    public void PointAt_GivenMidpoint_InterpolatesPosition()
    {
        var path = new PetEntrancePath(
            new PetPoint(0, 0),
            new PetPoint(100, 200),
            TimeSpan.FromSeconds(1));

        Assert.Equal(new PetPoint(50, 100), path.PointAt(0.5d));
    }

    [Theory]
    [InlineData(-1d, 0, 0)]
    [InlineData(2d, 100, 200)]
    public void PointAt_GivenOutOfRangeProgress_ClampsToEndpoint(
        double progress,
        int expectedX,
        int expectedY)
    {
        var path = new PetEntrancePath(
            new PetPoint(0, 0),
            new PetPoint(100, 200),
            TimeSpan.FromSeconds(1));

        Assert.Equal(new PetPoint(expectedX, expectedY), path.PointAt(progress));
    }

    [Fact]
    public void GetScreenEdgeStart_GivenLanding_ClearsTheRightEdgeAtLandingHeight()
    {
        PetPoint start = PetPlacement.GetScreenEdgeStart(_primary, new PetPoint(1736, 856), pixelSize: 160);

        Assert.Equal(new PetPoint(2080, 856), start);
    }

    [Fact]
    public void CreateEntrancePath_GivenScreenEdgeEntry_TakesAboutEightTenthsOfASecond()
    {
        var landing = new PetPoint(1736, 856);
        PetPoint start = PetPlacement.GetScreenEdgeStart(_primary, landing, pixelSize: 160);

        PetEntrancePath path = PetPlacement.CreateEntrancePath(start, landing, scaling: 1d);

        Assert.InRange(path.Duration.TotalSeconds, 0.75d, 0.77d);
    }

    [Fact]
    public void GetDefault_GivenNullWorkAreas_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PetPlacement.GetDefault(null!));
    }

    [Fact]
    public void Resolve_GivenEmptyWorkAreas_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PetPlacement.Resolve(new PetPoint(0, 0), []));
    }
}
