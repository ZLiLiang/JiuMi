namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The horizontal head-tracking direction while the cat sits and watches the cursor.
/// </summary>
public enum MyJiuMiSitWatchDirection
{
    /// <summary>
    /// Looking left toward a cursor to the left of the cat's head.
    /// </summary>
    Left,

    /// <summary>
    /// Looking forward or slightly upward toward a cursor near the cat's head.
    /// </summary>
    Center,

    /// <summary>
    /// Looking right toward a cursor to the right of the cat's head.
    /// </summary>
    Right,
}
