namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// Clamps the normalized horizontal position of the cat between the room's left and right edges.
/// </summary>
public static class MyJiuMiPositionRules
{
    /// <summary>
    /// Gets the position used before the user has dragged the cat.
    /// </summary>
    public const double Default = 0.5;

    /// <summary>
    /// Clamps a stored or computed position to <c>0</c>–<c>1</c>, replacing a non-finite value
    /// with the default.
    /// </summary>
    /// <param name="value">The normalized horizontal position.</param>
    /// <returns>A finite value in <c>[0, 1]</c>.</returns>
    public static double Clamp(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : Default;

    /// <summary>
    /// Reports whether a parsed value already lies in <c>[0, 1]</c>.
    /// </summary>
    /// <param name="value">The parsed value.</param>
    /// <returns><see langword="true"/> when the value needs no clamping.</returns>
    public static bool IsInRange(double value) => double.IsFinite(value) && value is >= 0 and <= 1;
}
