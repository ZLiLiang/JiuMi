namespace JiuMi.Application.Pet;

/// <summary>
/// A straight entrance path from the off-screen start to the landing position.
/// </summary>
/// <param name="Start">The starting top-left position in physical pixels.</param>
/// <param name="End">The landing top-left position in physical pixels.</param>
/// <param name="Duration">The time the move takes at the fixed entrance speed.</param>
public readonly record struct PetEntrancePath(PetPoint Start, PetPoint End, TimeSpan Duration)
{
    /// <summary>
    /// Gets the window position at a point on the path.
    /// </summary>
    /// <param name="progress">The fraction of the path, clamped to the 0–1 range.</param>
    /// <returns>The interpolated top-left position.</returns>
    public PetPoint PointAt(double progress)
    {
        double clamped = Math.Clamp(progress, 0d, 1d);
        return new PetPoint(
            Interpolate(Start.X, End.X, clamped),
            Interpolate(Start.Y, End.Y, clamped));
    }

    private static int Interpolate(int start, int end, double progress) =>
        (int)Math.Round(start + ((end - start) * progress), MidpointRounding.AwayFromZero);
}
