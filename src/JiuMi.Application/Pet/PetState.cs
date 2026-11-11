namespace JiuMi.Application.Pet;

/// <summary>
/// The animation states of the desktop pet.
/// </summary>
public enum PetState
{
    /// <summary>
    /// Breathing and tail sway; the resting loop.
    /// </summary>
    Idle,

    /// <summary>
    /// A short eye blink played from <see cref="Idle"/>.
    /// </summary>
    Blink,

    /// <summary>
    /// Ears up and eyes wide after a click, followed by the heart overlay.
    /// </summary>
    React,

    /// <summary>
    /// Dangling while the window is dragged; loops until the drag ends.
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
    /// The walking loop that plays while the window enters the desktop.
    /// </summary>
    WalkOut,
}
