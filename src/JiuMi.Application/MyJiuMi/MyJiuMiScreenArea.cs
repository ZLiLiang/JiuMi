namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The usable area of one screen, in physical pixels, and its device scale.
/// </summary>
/// <param name="X">The left edge of the work area.</param>
/// <param name="Y">The top edge of the work area.</param>
/// <param name="Width">The work area width in physical pixels.</param>
/// <param name="Height">The work area height in physical pixels.</param>
/// <param name="Scaling">The screen scale factor where 1.0 is 100%.</param>
/// <param name="IsPrimary">Whether this is the primary screen.</param>
public readonly record struct MyJiuMiScreenArea(
    int X,
    int Y,
    int Width,
    int Height,
    double Scaling,
    bool IsPrimary)
{
    /// <summary>
    /// Gets the right edge of the work area.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// Gets the bottom edge of the work area.
    /// </summary>
    public int Bottom => Y + Height;
}
