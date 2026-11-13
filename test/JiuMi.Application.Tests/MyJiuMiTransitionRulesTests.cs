using JiuMi.Application.MyJiuMi;

namespace JiuMi.Application.Tests;

/// <summary>
/// The session-independent desktop rules: when a cat may walk out, where the 160 DIP window lands,
/// and how its position falls back into the nearest screen work area.
/// </summary>
public sealed class MyJiuMiTransitionRulesTests
{
    private static readonly MyJiuMiScreenArea _primary =
        new(0, 0, 1920, 1080, 1d, IsPrimary: true);

    [Theory]
    [InlineData(MyJiuMiLeaveTrigger.HiddenToTray)]
    [InlineData(MyJiuMiLeaveTrigger.Minimized)]
    public void ShouldLeavePage_GivenAValidHideTrigger_ReturnsTrue(MyJiuMiLeaveTrigger trigger)
    {
        Assert.True(MyJiuMiTransitionRules.ShouldLeavePage(
            trigger,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: true));
    }

    [Theory]
    [InlineData(MyJiuMiLeaveTrigger.Exiting)]
    [InlineData(MyJiuMiLeaveTrigger.PageLeft)]
    public void ShouldLeavePage_GivenAnExitOrPageChange_ReturnsFalse(MyJiuMiLeaveTrigger trigger)
    {
        Assert.False(MyJiuMiTransitionRules.ShouldLeavePage(
            trigger,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: true));
    }

    [Fact]
    public void ShouldLeavePage_GivenASleepingCat_ReturnsFalse()
    {
        Assert.False(MyJiuMiTransitionRules.ShouldLeavePage(
            MyJiuMiLeaveTrigger.HiddenToTray,
            pageCurrent: true,
            catAwake: false,
            desktopAvailable: true));
    }

    [Fact]
    public void ShouldLeavePage_GivenAnotherPage_ReturnsFalse()
    {
        Assert.False(MyJiuMiTransitionRules.ShouldLeavePage(
            MyJiuMiLeaveTrigger.Minimized,
            pageCurrent: false,
            catAwake: true,
            desktopAvailable: true));
    }

    [Fact]
    public void ShouldLeavePage_GivenNoDesktopHost_ReturnsFalse()
    {
        Assert.False(MyJiuMiTransitionRules.ShouldLeavePage(
            MyJiuMiLeaveTrigger.Minimized,
            pageCurrent: true,
            catAwake: true,
            desktopAvailable: false));
    }

    [Fact]
    public void ShouldLeavePage_GivenADraggedOutSleepingCat_ReturnsTrue()
    {
        Assert.True(MyJiuMiTransitionRules.ShouldLeavePage(
            MyJiuMiLeaveTrigger.DraggedOut,
            pageCurrent: true,
            catAwake: false,
            desktopAvailable: true));
    }

    [Fact]
    public void ShouldLeavePage_GivenADraggedOutCatOnAnotherPage_ReturnsFalse()
    {
        Assert.False(MyJiuMiTransitionRules.ShouldLeavePage(
            MyJiuMiLeaveTrigger.DraggedOut,
            pageCurrent: false,
            catAwake: true,
            desktopAvailable: true));
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0.25, 0)]
    [InlineData(0.49, 0)]
    [InlineData(0.5, 1)]
    [InlineData(0.75, 1)]
    [InlineData(2, 1)]
    public void NearestEdge_GivenAPosition_ReturnsTheCloserRoomEdge(double positionX, double expected)
    {
        Assert.Equal(expected, MyJiuMiTransitionRules.NearestEdge(positionX));
    }

    [Theory]
    [InlineData(1, 160)]
    [InlineData(1.25, 200)]
    [InlineData(1.5, 240)]
    public void PixelSize_GivenAScaling_ReturnsThePhysicalWindowSize(double scaling, int expected)
    {
        Assert.Equal(expected, MyJiuMiTransitionRules.PixelSize(scaling));
    }

    [Fact]
    public void PlaceFromAnchor_GivenA100PercentScreen_CentersTheWindowOnTheAnchor()
    {
        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.PlaceFromAnchor(
            new MyJiuMiScreenPoint(500, 600),
            [_primary]);

        Assert.Equal(new MyJiuMiScreenPoint(420, 440), placement.Origin);
        Assert.Equal(_primary, placement.Area);
        Assert.Equal(160, placement.PixelSize);
    }

    [Fact]
    public void PlaceFromAnchor_GivenThe200DipEntrance_CentersTheLargerWindow()
    {
        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.PlaceFromAnchor(
            new MyJiuMiScreenPoint(500, 600),
            200,
            [_primary]);

        Assert.Equal(new MyJiuMiScreenPoint(400, 400), placement.Origin);
        Assert.Equal(200, placement.PixelSize);
    }

    [Fact]
    public void PlaceFromAnchor_GivenA150PercentScreen_ScalesTheOffset()
    {
        var scaled = new MyJiuMiScreenArea(0, 0, 2560, 1440, 1.5d, IsPrimary: true);

        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.PlaceFromAnchor(
            new MyJiuMiScreenPoint(900, 900),
            [scaled]);

        Assert.Equal(new MyJiuMiScreenPoint(780, 660), placement.Origin);
        Assert.Equal(240, placement.PixelSize);
    }

    [Fact]
    public void PlaceFromAnchor_GivenAnAnchorNearTheCorner_ClampsIntoTheWorkArea()
    {
        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.PlaceFromAnchor(
            new MyJiuMiScreenPoint(20, 20),
            [_primary]);

        Assert.Equal(new MyJiuMiScreenPoint(0, 0), placement.Origin);
    }

    [Fact]
    public void PlaceFromAnchor_GivenASecondaryScreen_UsesItsNegativeCoordinatesAndScaling()
    {
        var secondary = new MyJiuMiScreenArea(-1920, 0, 1920, 1080, 1.25d, IsPrimary: false);

        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.PlaceFromAnchor(
            new MyJiuMiScreenPoint(-1000, 600),
            [secondary, _primary]);

        Assert.Equal(new MyJiuMiScreenPoint(-1100, 400), placement.Origin);
        Assert.Equal(secondary, placement.Area);
        Assert.Equal(200, placement.PixelSize);
    }

    [Fact]
    public void ResolveOrigin_GivenAWindowOffScreen_ReturnsTheNearestWorkArea()
    {
        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.ResolveOrigin(
            new MyJiuMiScreenPoint(4000, 3000),
            [_primary]);

        Assert.Equal(new MyJiuMiScreenPoint(1760, 920), placement.Origin);
        Assert.Equal(_primary, placement.Area);
    }

    [Fact]
    public void ResolveOrigin_GivenAWindowOnTheSecondaryScreen_KeepsItThere()
    {
        var secondary = new MyJiuMiScreenArea(-1920, 0, 1920, 1080, 1d, IsPrimary: false);
        var desired = new MyJiuMiScreenPoint(-1200, 300);

        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.ResolveOrigin(
            desired,
            [secondary, _primary]);

        Assert.Equal(desired, placement.Origin);
        Assert.Equal(secondary, placement.Area);
    }

    [Fact]
    public void ResolveOrigin_WithoutWorkAreas_Throws()
    {
        Assert.Throws<ArgumentException>(() => MyJiuMiTransitionRules.ResolveOrigin(
            new MyJiuMiScreenPoint(0, 0),
            []));
    }

    [Fact]
    public void DragHandoffDuration_Is120Milliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(120), MyJiuMiTransitionRules.DragHandoffDuration);
    }

    [Fact]
    public void ShouldCommitDragOut_GivenAPointerInsideTheRoom_ReturnsFalse()
    {
        Assert.False(MyJiuMiTransitionRules.ShouldCommitDragOut(
            Room,
            new MyJiuMiScreenPoint(300, 250),
            scaling: 1));
    }

    [Fact]
    public void ShouldCommitDragOut_GivenAPointer47DipOutside_ReturnsFalse()
    {
        Assert.False(MyJiuMiTransitionRules.ShouldCommitDragOut(
            Room,
            new MyJiuMiScreenPoint(547, 250),
            scaling: 1));
    }

    [Fact]
    public void ShouldCommitDragOut_GivenAPointer48DipOutside_ReturnsTrue()
    {
        Assert.True(MyJiuMiTransitionRules.ShouldCommitDragOut(
            Room,
            new MyJiuMiScreenPoint(548, 250),
            scaling: 1));
    }

    [Fact]
    public void ShouldCommitDragOut_GivenAPointerAboveTheRoom_ReturnsTrue()
    {
        Assert.True(MyJiuMiTransitionRules.ShouldCommitDragOut(
            Room,
            new MyJiuMiScreenPoint(300, 52),
            scaling: 1));
    }

    [Fact]
    public void ShouldCommitDragOut_GivenA150PercentScreen_UsesTheScaledThreshold()
    {
        Assert.False(MyJiuMiTransitionRules.ShouldCommitDragOut(
            Room,
            new MyJiuMiScreenPoint(571, 250),
            scaling: 1.5));

        Assert.True(MyJiuMiTransitionRules.ShouldCommitDragOut(
            Room,
            new MyJiuMiScreenPoint(572, 250),
            scaling: 1.5));
    }

    [Fact]
    public void ShouldCommitDragOut_GivenAnEmptyRoom_ReturnsFalse()
    {
        Assert.False(MyJiuMiTransitionRules.ShouldCommitDragOut(
            new MyJiuMiScreenRect(100, 100, 0, 300),
            new MyJiuMiScreenPoint(900, 900),
            scaling: 1));
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(300, 0.5)]
    [InlineData(500, 1)]
    [InlineData(0, 0)]
    [InlineData(900, 1)]
    public void ProjectRoomX_GivenAScreenX_ReturnsTheClampedNormalizedPosition(int x, double expected)
    {
        Assert.Equal(expected, MyJiuMiTransitionRules.ProjectRoomX(Room, x));
    }

    [Fact]
    public void TryProjectRoomX_GivenAReleaseInsideTheRoom_UsesTheCatAnchorX()
    {
        bool projected = MyJiuMiTransitionRules.TryProjectRoomX(
            Room,
            new MyJiuMiScreenPoint(350, 380),
            350,
            out double positionX);

        Assert.True(projected);
        Assert.Equal(0.625, positionX);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(900, 1)]
    public void TryProjectRoomX_GivenAnAnchorOutsideTheRoom_ClampsTheNormalizedPosition(
        int anchorX,
        double expected)
    {
        bool projected = MyJiuMiTransitionRules.TryProjectRoomX(
            Room,
            new MyJiuMiScreenPoint(300, 380),
            anchorX,
            out double positionX);

        Assert.True(projected);
        Assert.Equal(expected, positionX);
    }

    [Fact]
    public void TryProjectRoomX_GivenAReleaseOutsideTheRoom_ReturnsFalse()
    {
        Assert.False(MyJiuMiTransitionRules.TryProjectRoomX(
            Room,
            new MyJiuMiScreenPoint(300, 50),
            300,
            out _));

        Assert.False(MyJiuMiTransitionRules.TryProjectRoomX(
            Room,
            new MyJiuMiScreenPoint(520, 250),
            520,
            out _));
    }

    private static MyJiuMiScreenRect Room => new(100, 100, 400, 300);
}
