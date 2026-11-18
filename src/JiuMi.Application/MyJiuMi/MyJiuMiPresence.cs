namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// Where the cat currently belongs: the page (home), a transition, or the desktop host.
/// </summary>
public enum MyJiuMiPresence
{
    /// <summary>
    /// The cat lives in the My JiuMi page; the default and the state after returning.
    /// </summary>
    Home,

    /// <summary>
    /// The cat is walking out of the page; a single handoff is in flight.
    /// </summary>
    Leaving,

    /// <summary>
    /// The cat lives in the desktop window.
    /// </summary>
    Desktop,

    /// <summary>
    /// The cat is returning to the page; a single handoff is in flight.
    /// </summary>
    Returning,
}
