namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The directional tracking capability tier supported by a character pack's sit-watch interaction slots.
/// </summary>
public enum MyJiuMiSitWatchTier
{
    /// <summary>
    /// Sit-watch is unavailable; the cat degrades to <see cref="MyJiuMiState.LyingIdle"/>.
    /// </summary>
    None = 0,

    /// <summary>
    /// Horizontal three-way tracking (<c>left</c>, <c>center</c>, <c>right</c>).
    /// </summary>
    ThreeWay = 3,

    /// <summary>
    /// Cardinal five-way tracking (<c>center</c>, <c>up</c>, <c>right</c>, <c>down</c>, <c>left</c>).
    /// </summary>
    FiveWay = 5,

    /// <summary>
    /// Full 360-degree nine-way tracking (<c>center</c> plus all eight 45-degree sectors).
    /// </summary>
    NineWay = 9,
}
