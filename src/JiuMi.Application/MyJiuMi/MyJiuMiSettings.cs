namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The persisted My JiuMi state: the cat's normalized horizontal position on the floor.
/// </summary>
public sealed record MyJiuMiSettings
{
    /// <summary>
    /// Gets the cat's horizontal position as a fraction of the room width, <c>0</c> left to
    /// <c>1</c> right.
    /// </summary>
    public double X
    {
        get;
        init;
    } = MyJiuMiPosition.Default;

    /// <summary>
    /// Creates the settings used before the user has dragged the cat.
    /// </summary>
    /// <returns>The default centered position.</returns>
    public static MyJiuMiSettings CreateDefault() => new();
}
