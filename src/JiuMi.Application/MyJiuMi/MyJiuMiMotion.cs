namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// One frame of the cat's procedural micro-motion: the transform and floor shadow applied by a host.
/// </summary>
/// <param name="ScaleX">The horizontal body scale, excluding the host's own handoff scale.</param>
/// <param name="ScaleY">The vertical body scale, excluding the host's own handoff scale.</param>
/// <param name="RotationDegrees">The whole-body sway around the bottom centre.</param>
/// <param name="OffsetY">The vertical offset in DIP; negative values lift the cat off the floor.</param>
/// <param name="ShadowScale">The uniform floor-shadow scale.</param>
/// <param name="ShadowOpacity">The floor-shadow opacity from 0 to 1.</param>
public readonly record struct MyJiuMiMotion(
    double ScaleX,
    double ScaleY,
    double RotationDegrees,
    double OffsetY,
    double ShadowScale,
    double ShadowOpacity)
{
    /// <summary>
    /// Gets the rest pose: no scale, rotation, lift or shadow change.
    /// </summary>
    public static MyJiuMiMotion None { get; } = new(1, 1, 0, 0, 1, 1);
}
