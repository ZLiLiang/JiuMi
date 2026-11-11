namespace JiuMi.Application.Pet;

/// <summary>
/// The frame table of every desktop pet animation.
/// </summary>
/// <remarks>
/// The table mirrors <c>docs/pet-art-spec.md</c> and the exported PNG folders; a frame timing change
/// belongs here and in the art spec together.
/// </remarks>
public static class PetAnimationCatalog
{
    private static readonly PetAnimationClip _idle = Create("idle", 12, 110, loops: true);
    private static readonly PetAnimationClip _blink = Create("blink", 3, 80, loops: false);
    private static readonly PetAnimationClip _react = Create("react", 3, 80, loops: false);
    private static readonly PetAnimationClip _drag = Create("drag", 2, 150, loops: true);
    private static readonly PetAnimationClip _sleepIn = Create("sleep-in", 4, 120, loops: false);
    private static readonly PetAnimationClip _sleep = Create("sleep", 4, 300, loops: true);
    private static readonly PetAnimationClip _wake = Create("wake", 2, 100, loops: false);
    private static readonly PetAnimationClip _walkOut = Create("walk", 8, 100, loops: true);

    /// <summary>
    /// Gets the frames, timing and looping rule of a state.
    /// </summary>
    /// <param name="state">The animation state.</param>
    /// <returns>The matching clip.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The state has no clip.</exception>
    public static PetAnimationClip For(PetState state) => state switch
    {
        PetState.Idle => _idle,
        PetState.Blink => _blink,
        PetState.React => _react,
        PetState.Drag => _drag,
        PetState.SleepIn => _sleepIn,
        PetState.Sleep => _sleep,
        PetState.Wake => _wake,
        PetState.WalkOut => _walkOut,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "No pet animation clip exists."),
    };

    private static PetAnimationClip Create(string folder, int frameCount, int milliseconds, bool loops)
    {
        var durations = new TimeSpan[frameCount];
        Array.Fill(durations, TimeSpan.FromMilliseconds(milliseconds));
        return new PetAnimationClip(folder, durations, loops);
    }
}
