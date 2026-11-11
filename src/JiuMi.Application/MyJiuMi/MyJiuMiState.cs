namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The animation states of the cat on the My JiuMi page.
/// </summary>
public enum MyJiuMiState
{
    /// <summary>
    /// Lying on the floor, breathing and swaying its tail; the resting loop.
    /// </summary>
    LyingIdle,

    /// <summary>
    /// A short eye blink played from <see cref="LyingIdle"/>.
    /// </summary>
    Blink,

    /// <summary>
    /// Ears up and eyes open after a click; the heart overlay follows.
    /// </summary>
    React,

    /// <summary>
    /// Dangling while the pointer drags the cat; loops until the drag ends.
    /// </summary>
    Drag,

    /// <summary>
    /// Curling down before sleep; plays once.
    /// </summary>
    SleepIn,

    /// <summary>
    /// Sleeping with slow breathing and the <c>Zzz</c> overlay; loops.
    /// </summary>
    Sleep,

    /// <summary>
    /// Standing up after sleep; plays once.
    /// </summary>
    Wake,

    /// <summary>
    /// Stretching from lying down into the walking pose; plays once.
    /// </summary>
    Stretch,

    /// <summary>
    /// The walking loop used by wandering and summoning; mirrors for the other direction.
    /// </summary>
    Walk,
}
