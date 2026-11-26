using System.Collections.ObjectModel;
using System.Globalization;

namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The fallback frame table of every My JiuMi animation, used when no character pack manifest is
/// supplied or when manifest loading degrades.
/// </summary>
/// <remarks>
/// The table mirrors <c>docs/pet-art-spec.md</c> and the built-in <c>Assets/MyJiuMi/manifest.json</c>.
/// </remarks>
public static class MyJiuMiAnimationCatalog
{
    private static readonly MyJiuMiPetPackCanvas _defaultCanvas = new(400, 400);
    private static readonly MyJiuMiPetPackAnchor _defaultAnchor = new(200, 399);
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
    private static readonly MyJiuMiAnimationClip _summonWalk = Create(
        "walk",
        33,
        MyJiuMiInteractionRules.SummonWalkFrameMilliseconds,
        loops: true);

    /// <summary>
    /// Gets the fast walk used by the summon: the same frames at the 1.6x pace.
    /// </summary>
    public static MyJiuMiAnimationClip SummonWalk => _summonWalk;

    /// <summary>
    /// Builds a fallback <see cref="MyJiuMiPetPackManifest"/> matching the frozen v1 orange-cat frame plan.
    /// </summary>
    /// <returns>A validated fallback manifest with <c>defaultAnchor = (200, 399)</c>.</returns>
    public static MyJiuMiPetPackManifest CreateFallbackManifest()
    {
        var clips = new Dictionary<string, MyJiuMiPetPackClip>(StringComparer.Ordinal);
        foreach (MyJiuMiState state in Enum.GetValues<MyJiuMiState>())
        {
            MyJiuMiAnimationClip legacy = GetClip(state);
            string key = MyJiuMiPetPackManifest.GetClipKey(state);
            var frames = new List<MyJiuMiPetPackFrame>(legacy.FrameDurations.Count);
            for (int index = 0; index < legacy.FrameDurations.Count; index++)
            {
                frames.Add(new MyJiuMiPetPackFrame(
                    string.Create(CultureInfo.InvariantCulture, $"{legacy.AssetFolder}/{index:D2}.png"),
                    legacy.FrameDurations[index],
                    _defaultAnchor));
            }

            clips[key] = new MyJiuMiPetPackClip(key, frames.AsReadOnly(), legacy.Loops, _defaultAnchor);
        }

        clips[MyJiuMiPetPackManifest.JumpClipKey] = new MyJiuMiPetPackClip(
            MyJiuMiPetPackManifest.JumpClipKey,
            [
                new MyJiuMiPetPackFrame("jump/airborne.png", TimeSpan.FromMilliseconds(504), _defaultAnchor),
                new MyJiuMiPetPackFrame("jump/landing.png", TimeSpan.FromMilliseconds(126), _defaultAnchor),
            ],
            Loops: false,
            _defaultAnchor);

        return new MyJiuMiPetPackManifest(
            MyJiuMiPetPackManifestLoader.SupportedSchemaVersion,
            "jiumi-orange-cat",
            "JiuMi Orange Cat",
            _defaultCanvas,
            _defaultAnchor,
            new ReadOnlyDictionary<string, MyJiuMiPetPackClip>(clips),
            new ReadOnlyDictionary<string, MyJiuMiPetPackClip>(
                new Dictionary<string, MyJiuMiPetPackClip>(StringComparer.Ordinal)),
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Creates a summon-pace copy of a walk clip using <see cref="MyJiuMiInteractionRules.SummonWalkFrameMilliseconds"/>.
    /// </summary>
    /// <param name="walkClip">The manifest walk clip.</param>
    /// <returns>A looping clip with the same frame count at summon pace.</returns>
    public static MyJiuMiAnimationClip CreateSummonWalk(MyJiuMiPetPackClip walkClip)
    {
        ArgumentNullException.ThrowIfNull(walkClip);
        return Create(
            walkClip.Key,
            walkClip.Frames.Count,
            MyJiuMiInteractionRules.SummonWalkFrameMilliseconds,
            loops: true);
    }

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
