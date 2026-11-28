namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The 360-degree head-tracking direction while the cat sits and watches the cursor
/// (centre plus eight 45-degree sectors).
/// </summary>
public enum MyJiuMiSitWatchDirection
{
    /// <summary>
    /// Looking left toward a cursor to the left of the cat's head.
    /// </summary>
    Left,

    /// <summary>
    /// Looking forward toward a cursor near the cat's head.
    /// </summary>
    Center,

    /// <summary>
    /// Looking right toward a cursor to the right of the cat's head.
    /// </summary>
    Right,

    /// <summary>
    /// Looking upward toward a cursor directly above the cat's head.
    /// </summary>
    Up,

    /// <summary>
    /// Looking downward toward a cursor directly below the cat's head.
    /// </summary>
    Down,

    /// <summary>
    /// Looking up and left toward a cursor in the upper-left sector.
    /// </summary>
    UpLeft,

    /// <summary>
    /// Looking up and right toward a cursor in the upper-right sector.
    /// </summary>
    UpRight,

    /// <summary>
    /// Looking down and left toward a cursor in the lower-left sector.
    /// </summary>
    DownLeft,

    /// <summary>
    /// Looking down and right toward a cursor in the lower-right sector.
    /// </summary>
    DownRight,
}
