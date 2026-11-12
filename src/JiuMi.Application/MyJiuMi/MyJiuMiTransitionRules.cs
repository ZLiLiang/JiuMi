namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The pure desktop rules that do not depend on a host: when the cat walks out, where the desktop
/// window lands, and how its position is clamped into the nearest screen work area.
/// </summary>
/// <remarks>
/// Screen values are physical pixels; the window size stays 160 DIP and is converted with the scale
/// of the screen that receives it. The page/desktop anchoring uses the cat's bottom-centre.
/// </remarks>
public static class MyJiuMiTransitionRules
{
    /// <summary>
    /// The page cat slot size in device-independent pixels.
    /// </summary>
    public const double HomeSizeDip = 200;

    /// <summary>
    /// The desktop window size in device-independent pixels.
    /// </summary>
    public const double DesktopSizeDip = 160;

    /// <summary>
    /// The distance, in DIP, the cat walks into the desktop after the handoff.
    /// </summary>
    public const double DesktopWalkDistanceDip = 40;

    /// <summary>
    /// Gets the longest time hiding to the tray waits for the cat to reach the room edge.
    /// </summary>
    public static TimeSpan TrayHideTimeout { get; } = TimeSpan.FromMilliseconds(900);

    /// <summary>
    /// Gets the part of <see cref="TrayHideTimeout"/> reserved for creating and showing the desktop
    /// host, so the shell itself is hidden within the 900 ms budget.
    /// </summary>
    public static TimeSpan TrayHandoffReserve { get; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets how long the minimized path fades and scales the desktop window in.
    /// </summary>
    public static TimeSpan MinimizeEntrance { get; } = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Reports whether the cat may walk out for the given shell action.
    /// </summary>
    /// <param name="trigger">The action that is about to hide or minimize the shell.</param>
    /// <param name="pageCurrent">Whether the My JiuMi page is the page currently shown.</param>
    /// <param name="catAwake">Whether the cat is awake and available.</param>
    /// <param name="desktopAvailable">Whether a desktop host may be attempted.</param>
    /// <returns><see langword="true"/> when the cat should walk out instead of pausing at home.</returns>
    public static bool ShouldWalkOut(
        MyJiuMiLeaveTrigger trigger,
        bool pageCurrent,
        bool catAwake,
        bool desktopAvailable) =>
        trigger is MyJiuMiLeaveTrigger.HiddenToTray or MyJiuMiLeaveTrigger.Minimized &&
        pageCurrent &&
        catAwake &&
        desktopAvailable;

    /// <summary>
    /// Gets the room edge nearest to a normalized position: <c>0</c> for the left edge and <c>1</c>
    /// for the right edge.
    /// </summary>
    /// <param name="positionX">The normalized cat centre.</param>
    /// <returns>The normalized edge the cat walks to.</returns>
    public static double NearestEdge(double positionX) =>
        MyJiuMiPositionRules.Clamp(positionX) < 0.5 ? 0 : 1;

    /// <summary>
    /// Converts a window size in DIP to physical pixels on a screen.
    /// </summary>
    /// <param name="sizeDip">The window size in device-independent pixels.</param>
    /// <param name="scaling">The screen scale factor where 1.0 is 100%.</param>
    /// <returns>The window size in physical pixels.</returns>
    public static int PixelSize(double sizeDip, double scaling) =>
        (int)Math.Round(
            sizeDip * Math.Max(scaling, 0.01d),
            MidpointRounding.AwayFromZero);

    /// <summary>
    /// Converts the fixed 160 DIP desktop window size to physical pixels on a screen.
    /// </summary>
    /// <param name="scaling">The screen scale factor where 1.0 is 100%.</param>
    /// <returns>The window size in physical pixels.</returns>
    public static int PixelSize(double scaling) => PixelSize(DesktopSizeDip, scaling);

    /// <summary>
    /// Places the desktop window so the cat's bottom-centre sits on <paramref name="anchor"/>.
    /// </summary>
    /// <param name="anchor">The bottom-centre anchor in physical screen pixels.</param>
    /// <param name="workAreas">Every screen work area, at least one.</param>
    /// <returns>The clamped window origin and the screen that received it.</returns>
    public static MyJiuMiDesktopPlacement PlaceFromAnchor(
        MyJiuMiScreenPoint anchor,
        IReadOnlyList<MyJiuMiScreenArea> workAreas) =>
        PlaceFromAnchor(anchor, DesktopSizeDip, workAreas);

    /// <summary>
    /// Places a desktop window of any size so the cat's bottom-centre sits on
    /// <paramref name="anchor"/>; used for the 200 to 160 DIP entrance.
    /// </summary>
    /// <param name="anchor">The bottom-centre anchor in physical screen pixels.</param>
    /// <param name="sizeDip">The window size in device-independent pixels.</param>
    /// <param name="workAreas">Every screen work area, at least one.</param>
    /// <returns>The clamped window origin and the screen that received it.</returns>
    public static MyJiuMiDesktopPlacement PlaceFromAnchor(
        MyJiuMiScreenPoint anchor,
        double sizeDip,
        IReadOnlyList<MyJiuMiScreenArea> workAreas)
    {
        MyJiuMiScreenArea area = SelectArea(anchor, workAreas);
        int pixelSize = PixelSize(sizeDip, area.Scaling);
        var desired = new MyJiuMiScreenPoint(
            anchor.X - (pixelSize / 2),
            anchor.Y - pixelSize);
        return new MyJiuMiDesktopPlacement(Clamp(desired, area, pixelSize), area, pixelSize);
    }

    /// <summary>
    /// Places a window at a desired origin, falling back into the nearest visible work area.
    /// </summary>
    /// <param name="desired">The desired top-left position in physical pixels.</param>
    /// <param name="workAreas">Every screen work area, at least one.</param>
    /// <returns>The clamped window origin and the screen that received it.</returns>
    public static MyJiuMiDesktopPlacement ResolveOrigin(
        MyJiuMiScreenPoint desired,
        IReadOnlyList<MyJiuMiScreenArea> workAreas)
    {
        Validate(workAreas);

        MyJiuMiScreenArea bestArea = workAreas[0];
        int bestPixelSize = PixelSize(bestArea.Scaling);
        MyJiuMiScreenPoint bestOrigin = Clamp(desired, bestArea, bestPixelSize);
        long bestDistance = DistanceSquared(bestOrigin, desired);

        foreach (MyJiuMiScreenArea area in workAreas.Skip(1))
        {
            int pixelSize = PixelSize(area.Scaling);
            MyJiuMiScreenPoint origin = Clamp(desired, area, pixelSize);
            long distance = DistanceSquared(origin, desired);
            if (distance < bestDistance ||
                (distance == bestDistance && area.IsPrimary && !bestArea.IsPrimary))
            {
                bestArea = area;
                bestPixelSize = pixelSize;
                bestOrigin = origin;
                bestDistance = distance;
            }
        }

        return new MyJiuMiDesktopPlacement(bestOrigin, bestArea, bestPixelSize);
    }

    private static MyJiuMiScreenArea SelectArea(
        MyJiuMiScreenPoint anchor,
        IReadOnlyList<MyJiuMiScreenArea> workAreas)
    {
        Validate(workAreas);

        foreach (MyJiuMiScreenArea area in workAreas)
        {
            if (anchor.X >= area.X &&
                anchor.X <= area.Right &&
                anchor.Y >= area.Y &&
                anchor.Y <= area.Bottom)
            {
                return area;
            }
        }

        MyJiuMiScreenArea bestArea = workAreas[0];
        long bestDistance = DistanceSquared(anchor, bestArea);
        foreach (MyJiuMiScreenArea area in workAreas.Skip(1))
        {
            long distance = DistanceSquared(anchor, area);
            if (distance < bestDistance ||
                (distance == bestDistance && area.IsPrimary && !bestArea.IsPrimary))
            {
                bestArea = area;
                bestDistance = distance;
            }
        }

        return bestArea;
    }

    private static MyJiuMiScreenPoint Clamp(
        MyJiuMiScreenPoint desired,
        MyJiuMiScreenArea area,
        int pixelSize)
    {
        int maxX = area.Right - pixelSize;
        int maxY = area.Bottom - pixelSize;
        int x = maxX < area.X ? area.X : Math.Clamp(desired.X, area.X, maxX);
        int y = maxY < area.Y ? area.Y : Math.Clamp(desired.Y, area.Y, maxY);
        return new MyJiuMiScreenPoint(x, y);
    }

    private static long DistanceSquared(MyJiuMiScreenPoint point, MyJiuMiScreenArea area)
    {
        long deltaX = point.X < area.X
            ? area.X - (long)point.X
            : point.X > area.Right
                ? point.X - (long)area.Right
                : 0;
        long deltaY = point.Y < area.Y
            ? area.Y - (long)point.Y
            : point.Y > area.Bottom
                ? point.Y - (long)area.Bottom
                : 0;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static long DistanceSquared(MyJiuMiScreenPoint left, MyJiuMiScreenPoint right)
    {
        long deltaX = left.X - (long)right.X;
        long deltaY = left.Y - (long)right.Y;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static void Validate(IReadOnlyList<MyJiuMiScreenArea> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0)
        {
            throw new ArgumentException("At least one work area is required.", nameof(workAreas));
        }
    }
}
