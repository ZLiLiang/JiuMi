namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The frame table of every My JiuMi animation.
/// </summary>
/// <remarks>
/// The table mirrors <c>docs/pet-art-spec.md</c> and the exported PNG folders; a frame timing change
/// belongs here and in the art spec together.
/// </remarks>
public static class MyJiuMiAnimationCatalog
{
    private static readonly MyJiuMiAnimationClip _lyingIdle = Create("lying-idle", 2, 1500, loops: true);
    private static readonly MyJiuMiAnimationClip _blink = Create(
        "blink",
        [
            TimeSpan.FromMilliseconds(60),
            TimeSpan.FromMilliseconds(90),
            TimeSpan.FromMilliseconds(60),
        ],
        loops: false);
    private static readonly MyJiuMiAnimationClip _react = Create("react", 2, 120, loops: false);
    private static readonly MyJiuMiAnimationClip _drag = Create("drag", 2, 400, loops: true);
    private static readonly MyJiuMiAnimationClip _sleepIn = Create("sleep-in", 3, 150, loops: false);
    private static readonly MyJiuMiAnimationClip _sleep = Create("sleep", 2, 2000, loops: true);
    private static readonly MyJiuMiAnimationClip _wake = Create("wake", 3, 150, loops: false);
    private static readonly MyJiuMiAnimationClip _stretch = Create("stretch", 4, 120, loops: false);
    private static readonly MyJiuMiAnimationClip _walk = Create("walk", 33, 26, loops: true);

    /// <summary>
    /// Gets the frames, timing and looping rule of a state.
    /// </summary>
    /// <param name="state">The animation state.</param>
    /// <returns>The matching clip.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The state has no clip.</exception>
    public static MyJiuMiAnimationClip GetClip(MyJiuMiState state) => state switch
    {
        MyJiuMiState.LyingIdle => _lyingIdle,
        MyJiuMiState.Blink => _blink,
        MyJiuMiState.React => _react,
        MyJiuMiState.Drag => _drag,
        MyJiuMiState.SleepIn => _sleepIn,
        MyJiuMiState.Sleep => _sleep,
        MyJiuMiState.Wake => _wake,
        MyJiuMiState.Stretch => _stretch,
        MyJiuMiState.Walk => _walk,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "No My JiuMi animation clip exists."),
    };

    private static MyJiuMiAnimationClip Create(string folder, int frameCount, int milliseconds, bool loops)
    {
        var durations = new TimeSpan[frameCount];
        Array.Fill(durations, TimeSpan.FromMilliseconds(milliseconds));
        return new MyJiuMiAnimationClip(folder, durations, loops);
    }

    private static MyJiuMiAnimationClip Create(
        string folder,
        IReadOnlyList<TimeSpan> durations,
        bool loops) => new(folder, durations, loops);
}
