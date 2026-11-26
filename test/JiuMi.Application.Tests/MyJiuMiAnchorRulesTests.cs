using JiuMi.Application.MyJiuMi;

namespace JiuMi.Application.Tests;

/// <summary>
/// Verifies anchor normalization and DIP offset calculation for page and desktop rendering.
/// </summary>
public sealed class MyJiuMiAnchorRulesTests
{
    private static readonly MyJiuMiPetPackCanvas _canvas = new(400, 400);

    [Fact]
    public void ComputePageFrameOffsetDip_GivenLegacyBottomCenterAnchor_IsZeroOffset()
    {
        var anchor = new MyJiuMiPetPackAnchor(200, 399);

        (double offsetX, double offsetY) = MyJiuMiAnchorRules.ComputePageFrameOffsetDip(
            anchor,
            _canvas,
            MyJiuMiInteractionRules.CatSize);

        Assert.Equal(0, offsetX, precision: 6);
        Assert.Equal(0, offsetY, precision: 6);
    }

    [Fact]
    public void ComputePageFrameOffsetDip_GivenV2FloorAnchor_ShiftsDownByFortyCanvasPixels()
    {
        var anchor = new MyJiuMiPetPackAnchor(200, 360);

        (double offsetX, double offsetY) = MyJiuMiAnchorRules.ComputePageFrameOffsetDip(
            anchor,
            _canvas,
            MyJiuMiInteractionRules.CatSize);

        Assert.Equal(0, offsetX, precision: 6);
        Assert.Equal(20, offsetY, precision: 6);
    }

    [Fact]
    public void ComputePageFrameOffsetDip_GivenShiftedHorizontalAnchor_CompensatesHorizontally()
    {
        var anchor = new MyJiuMiPetPackAnchor(210, 360);

        (double offsetX, double offsetY) = MyJiuMiAnchorRules.ComputePageFrameOffsetDip(
            anchor,
            _canvas,
            MyJiuMiInteractionRules.CatSize);

        Assert.Equal(-5, offsetX, precision: 6);
        Assert.Equal(20, offsetY, precision: 6);
    }

    [Fact]
    public void ComputeDesktopFrameDeltaDip_GivenPerFrameOverride_ReturnsRelativeOffset()
    {
        var defaultAnchor = new MyJiuMiPetPackAnchor(200, 360);
        var frameAnchor = new MyJiuMiPetPackAnchor(205, 365);

        (double offsetX, double offsetY) = MyJiuMiAnchorRules.ComputeDesktopFrameDeltaDip(
            frameAnchor,
            defaultAnchor,
            _canvas,
            MyJiuMiTransitionRules.DesktopSizeDip);

        Assert.Equal(-2, offsetX, precision: 6);
        Assert.Equal(-2, offsetY, precision: 6);
    }

    [Fact]
    public void PlaceFromAnchor_GivenV2FloorAnchor_PositionsWindowSoAnchorMeetsTargetPoint()
    {
        var anchor = new MyJiuMiPetPackAnchor(200, 360);
        (double normX, double normY) = MyJiuMiAnchorRules.Normalize(anchor, _canvas);
        MyJiuMiScreenArea[] workAreas = [new MyJiuMiScreenArea(0, 0, 1920, 1080, 1.0, IsPrimary: true)];

        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.PlaceFromAnchor(
            new MyJiuMiScreenPoint(500, 600),
            MyJiuMiTransitionRules.DesktopSizeDip,
            normX,
            normY,
            workAreas);

        // 160 * 0.5 = 80, 160 * 0.9 = 144 -> origin = (420, 456), leaving 16px below floor for drooping tail.
        Assert.Equal(new MyJiuMiScreenPoint(420, 456), placement.Origin);
    }
}
