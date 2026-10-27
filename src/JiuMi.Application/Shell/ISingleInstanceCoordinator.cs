namespace JiuMi.Application.Shell;

/// <summary>
/// Keeps one shell process per executable path and per user session, and carries the request a later
/// launch sends to the running instance.
/// </summary>
public interface ISingleInstanceCoordinator : IDisposable
{
    /// <summary>
    /// Raised on a background thread when another launch asks the running instance to come forward.
    /// </summary>
    event EventHandler? ActivationRequested;

    /// <summary>
    /// Acquires the instance lock, or asks the running instance to come forward and gives up.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> for the first instance. <see langword="false"/> when another instance owns
    /// the lock; the caller then exits without starting the interface.
    /// </returns>
    bool TryAcquire();
}
