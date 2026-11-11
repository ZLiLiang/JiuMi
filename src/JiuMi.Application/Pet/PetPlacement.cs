namespace JiuMi.Application.Pet;

/// <summary>
/// The window size, screen rules and entrance timing of the desktop pet.
/// </summary>
public static class PetPlacement
{
    /// <summary>
    /// The pet window size in device-independent pixels.
    /// </summary>
    public const int SizeDip = 160;

    /// <summary>
    /// The distance from the right and bottom work-area edges in device-independent pixels.
    /// </summary>
    public const int EdgeMarginDip = 24;

    /// <summary>
    /// The fixed entrance speed in device-independent pixels per second.
    /// </summary>
    public const double EntranceSpeedDipPerSecond = 450d;

    /// <summary>
    /// Converts the fixed window size to physical pixels on a screen.
    /// </summary>
    /// <param name="scaling">The screen scale factor where 1.0 is 100%.</param>
    /// <returns>The window size in physical pixels.</returns>
    public static int PixelSize(double scaling) =>
        (int)Math.Round(SizeDip * scaling, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Gets the first-run position in the bottom-right corner of the primary work area.
    /// </summary>
    /// <param name="workAreas">Every screen work area, at least one.</param>
    /// <returns>The default position with its screen.</returns>
    public static PetPlacementResult GetDefault(IReadOnlyList<PetWorkArea> workAreas)
    {
        ValidateWorkAreas(workAreas);

        PetWorkArea workArea = workAreas.FirstOrDefault(area => area.IsPrimary);
        if (!workArea.IsPrimary)
        {
            workArea = workAreas[0];
        }

        int pixelSize = PixelSize(workArea.Scaling);
        int margin = (int)Math.Round(EdgeMarginDip * workArea.Scaling, MidpointRounding.AwayFromZero);
        var desired = new PetPoint(
            workArea.Right - pixelSize - margin,
            workArea.Bottom - pixelSize - margin);
        return new PetPlacementResult(Clamp(desired, workArea, pixelSize), workArea, pixelSize);
    }

    /// <summary>
    /// Places a remembered position inside the nearest visible work area.
    /// </summary>
    /// <param name="desired">The desired top-left position in physical pixels.</param>
    /// <param name="workAreas">Every screen work area, at least one.</param>
    /// <returns>The clamped position and the screen that received it.</returns>
    public static PetPlacementResult Resolve(PetPoint desired, IReadOnlyList<PetWorkArea> workAreas)
    {
        ValidateWorkAreas(workAreas);

        PetWorkArea bestArea = workAreas[0];
        int bestPixelSize = PixelSize(bestArea.Scaling);
        PetPoint bestPosition = Clamp(desired, bestArea, bestPixelSize);
        long bestDistance = DistanceSquared(bestPosition, desired);

        foreach (PetWorkArea workArea in workAreas.Skip(1))
        {
            int pixelSize = PixelSize(workArea.Scaling);
            PetPoint position = Clamp(desired, workArea, pixelSize);
            long distance = DistanceSquared(position, desired);
            if (distance < bestDistance ||
                (distance == bestDistance && workArea.IsPrimary && !bestArea.IsPrimary))
            {
                bestArea = workArea;
                bestPixelSize = pixelSize;
                bestPosition = position;
                bestDistance = distance;
            }
        }

        return new PetPlacementResult(bestPosition, bestArea, bestPixelSize);
    }

    /// <summary>
    /// Gets the off-screen start point one window width beyond the right edge, at the landing height.
    /// </summary>
    /// <param name="workArea">The screen the pet lands on.</param>
    /// <param name="landing">The landing top-left position.</param>
    /// <param name="pixelSize">The pet window size in physical pixels.</param>
    /// <returns>The starting position fully beyond the right edge.</returns>
    public static PetPoint GetScreenEdgeStart(PetWorkArea workArea, PetPoint landing, int pixelSize) =>
        new(workArea.Right + pixelSize, landing.Y);

    /// <summary>
    /// Builds the straight entrance path at the fixed speed.
    /// </summary>
    /// <param name="start">The starting top-left position in physical pixels.</param>
    /// <param name="end">The landing top-left position in physical pixels.</param>
    /// <param name="scaling">The scale of the screen the path runs on.</param>
    /// <returns>The path and the time it takes.</returns>
    public static PetEntrancePath CreateEntrancePath(PetPoint start, PetPoint end, double scaling)
    {
        double deltaX = end.X - start.X;
        double deltaY = end.Y - start.Y;
        double physicalDistance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        double dipDistance = physicalDistance / Math.Max(scaling, 0.01d);
        return new PetEntrancePath(
            start,
            end,
            TimeSpan.FromSeconds(dipDistance / EntranceSpeedDipPerSecond));
    }

    private static void ValidateWorkAreas(IReadOnlyList<PetWorkArea> workAreas)
    {
        ArgumentNullException.ThrowIfNull(workAreas);
        if (workAreas.Count == 0)
        {
            throw new ArgumentException("At least one work area is required.", nameof(workAreas));
        }
    }

    private static PetPoint Clamp(PetPoint desired, PetWorkArea workArea, int pixelSize)
    {
        int maxX = workArea.Right - pixelSize;
        int maxY = workArea.Bottom - pixelSize;
        int x = maxX < workArea.X ? workArea.X : Math.Clamp(desired.X, workArea.X, maxX);
        int y = maxY < workArea.Y ? workArea.Y : Math.Clamp(desired.Y, workArea.Y, maxY);
        return new PetPoint(x, y);
    }

    private static long DistanceSquared(PetPoint left, PetPoint right)
    {
        long deltaX = left.X - (long)right.X;
        long deltaY = left.Y - (long)right.Y;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}
