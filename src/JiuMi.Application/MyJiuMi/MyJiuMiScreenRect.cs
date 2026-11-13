namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// A rectangle in physical screen pixels, used for the room the cat can be dragged back into.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
public readonly record struct MyJiuMiScreenRect(int X, int Y, int Width, int Height)
{
    /// <summary>
    /// Gets the right edge.
    /// </summary>
    public int Right => X + Width;

    /// <summary>
    /// Gets the bottom edge.
    /// </summary>
    public int Bottom => Y + Height;

    /// <summary>
    /// Gets a value indicating whether the rectangle has no visible area.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Reports whether a screen point lies inside the rectangle, edges included.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><see langword="true"/> when the point is inside.</returns>
    public bool Contains(MyJiuMiScreenPoint point) =>
        !IsEmpty &&
        point.X >= X &&
        point.X <= Right &&
        point.Y >= Y &&
        point.Y <= Bottom;
}
