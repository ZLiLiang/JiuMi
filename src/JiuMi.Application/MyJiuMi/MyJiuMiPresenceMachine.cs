namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// Owns the cat's ownership transitions between the page and the desktop, with the guards that keep
/// a sleeping cat or a hidden page at home.
/// </summary>
/// <remarks>
/// The machine is pure: the caller supplies the walk-out conditions and performs the host handoff
/// around <see cref="TryCompleteLeave"/> and <see cref="TryCompleteReturn"/>. Transitional states
/// allow only one in-flight handoff, so repeated hide, minimize or drag events are ignored.
/// </remarks>
public sealed class MyJiuMiPresenceMachine
{
    /// <summary>
    /// Gets where the cat currently belongs.
    /// </summary>
    public MyJiuMiPresence Presence { get; private set; } = MyJiuMiPresence.Home;

    /// <summary>
    /// Gets a value indicating whether a handoff is in flight.
    /// </summary>
    public bool IsTransitioning => Presence is MyJiuMiPresence.Leaving or MyJiuMiPresence.Returning;

    /// <summary>
    /// Starts the walk out of the page when the shell action and the cat allow it.
    /// </summary>
    /// <param name="trigger">The action that is about to hide or minimize the shell.</param>
    /// <param name="pageCurrent">Whether the My JiuMi page is the page currently shown.</param>
    /// <param name="catAwake">Whether the cat is awake and available.</param>
    /// <param name="desktopAvailable">Whether a desktop host may be attempted.</param>
    /// <returns><see langword="true"/> when the machine entered <see cref="MyJiuMiPresence.Leaving"/>.</returns>
    public bool TryBeginLeave(
        MyJiuMiLeaveTrigger trigger,
        bool pageCurrent,
        bool catAwake,
        bool desktopAvailable)
    {
        if (Presence != MyJiuMiPresence.Home ||
            !MyJiuMiTransitionRules.ShouldLeavePage(trigger, pageCurrent, catAwake, desktopAvailable))
        {
            return false;
        }

        Presence = MyJiuMiPresence.Leaving;
        return true;
    }

    /// <summary>
    /// Completes the walk out once the desktop host is on screen.
    /// </summary>
    /// <returns><see langword="true"/> when the machine entered <see cref="MyJiuMiPresence.Desktop"/>.</returns>
    public bool TryCompleteLeave()
    {
        if (Presence != MyJiuMiPresence.Leaving)
        {
            return false;
        }

        Presence = MyJiuMiPresence.Desktop;
        return true;
    }

    /// <summary>
    /// Cancels the walk out after a failed or interrupted handoff.
    /// </summary>
    /// <returns><see langword="true"/> when the machine returned to <see cref="MyJiuMiPresence.Home"/>.</returns>
    public bool TryCancelLeave()
    {
        if (Presence != MyJiuMiPresence.Leaving)
        {
            return false;
        }

        Presence = MyJiuMiPresence.Home;
        return true;
    }

    /// <summary>
    /// Starts the return to the page when the page can receive the cat.
    /// </summary>
    /// <param name="pageReachable">Whether the My JiuMi page is visible and can receive the cat.</param>
    /// <returns><see langword="true"/> when the machine entered <see cref="MyJiuMiPresence.Returning"/>.</returns>
    public bool TryBeginReturn(bool pageReachable)
    {
        if (!pageReachable || Presence != MyJiuMiPresence.Desktop)
        {
            return false;
        }

        Presence = MyJiuMiPresence.Returning;
        return true;
    }

    /// <summary>
    /// Completes the return once the page host shows the cat.
    /// </summary>
    /// <returns><see langword="true"/> when the machine entered <see cref="MyJiuMiPresence.Home"/>.</returns>
    public bool TryCompleteReturn()
    {
        if (Presence != MyJiuMiPresence.Returning)
        {
            return false;
        }

        Presence = MyJiuMiPresence.Home;
        return true;
    }

    /// <summary>
    /// Aborts the return after a failed handoff; the cat stays on the desktop.
    /// </summary>
    /// <returns><see langword="true"/> when the machine returned to <see cref="MyJiuMiPresence.Desktop"/>.</returns>
    public bool TryAbortReturn()
    {
        if (Presence != MyJiuMiPresence.Returning)
        {
            return false;
        }

        Presence = MyJiuMiPresence.Desktop;
        return true;
    }

    /// <summary>
    /// Returns to <see cref="MyJiuMiPresence.Home"/> from any state, used when the application exits.
    /// </summary>
    public void Reset() => Presence = MyJiuMiPresence.Home;
}
