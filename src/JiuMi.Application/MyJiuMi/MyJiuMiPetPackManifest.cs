namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// A validated JiuMi native character pack manifest (<c>schemaVersion: 1</c>).
/// </summary>
/// <param name="SchemaVersion">The manifest schema version.</param>
/// <param name="Id">The stable identifier of the character pack.</param>
/// <param name="Name">The display name of the character pack.</param>
/// <param name="Canvas">The pixel canvas dimensions shared by frames in this pack.</param>
/// <param name="DefaultAnchor">The pack-level fallback anchor when a clip or frame omits one.</param>
/// <param name="Clips">The core animation clips keyed by canonical state name.</param>
/// <param name="Interactions">Optional interaction extension clips; empty when omitted.</param>
/// <param name="Files">Optional pack-relative file SHA-256 checksums; empty when omitted.</param>
public sealed record MyJiuMiPetPackManifest(
    int SchemaVersion,
    string Id,
    string Name,
    MyJiuMiPetPackCanvas Canvas,
    MyJiuMiPetPackAnchor DefaultAnchor,
    IReadOnlyDictionary<string, MyJiuMiPetPackClip> Clips,
    IReadOnlyDictionary<string, MyJiuMiPetPackClip> Interactions,
    IReadOnlyDictionary<string, string> Files)
{
    /// <summary>
    /// The canonical manifest key for the click jump clip.
    /// </summary>
    public const string JumpClipKey = "jump";

    /// <summary>
    /// The optional interaction slot key for sitting and looking left toward the cursor.
    /// </summary>
    public const string SitWatchLeftInteractionKey = "sit-watch-left";

    /// <summary>
    /// The optional interaction slot key for sitting and looking forward toward the cursor.
    /// </summary>
    public const string SitWatchCenterInteractionKey = "sit-watch-center";

    /// <summary>
    /// The optional interaction slot key for sitting and looking right toward the cursor.
    /// </summary>
    public const string SitWatchRightInteractionKey = "sit-watch-right";

    /// <summary>
    /// The optional interaction slot key for transitioning from sitting to lying down.
    /// </summary>
    public const string SitToLieInteractionKey = "sit-to-lie";

    private static readonly MyJiuMiState[] _coreStates =
    [
        MyJiuMiState.LyingIdle,
        MyJiuMiState.Blink,
        MyJiuMiState.React,
        MyJiuMiState.Drag,
        MyJiuMiState.SleepIn,
        MyJiuMiState.Sleep,
        MyJiuMiState.Wake,
        MyJiuMiState.Stretch,
        MyJiuMiState.Walk,
    ];

    /// <summary>
    /// Gets the nine core animation states required in every <c>schemaVersion: 1</c> manifest.
    /// </summary>
    public static IReadOnlyList<MyJiuMiState> CoreStates => _coreStates;

    /// <summary>
    /// Gets a value indicating whether this character pack declares all three directional sit-watch
    /// interaction slots (<c>sit-watch-left</c>, <c>sit-watch-center</c> and <c>sit-watch-right</c>).
    /// </summary>
    public bool SupportsSitWatch =>
        Interactions.ContainsKey(SitWatchLeftInteractionKey) &&
        Interactions.ContainsKey(SitWatchCenterInteractionKey) &&
        Interactions.ContainsKey(SitWatchRightInteractionKey);

    /// <summary>
    /// Gets the interaction slot key for a <see cref="MyJiuMiSitWatchDirection"/>.
    /// </summary>
    /// <param name="direction">The sit-watch gaze direction.</param>
    /// <returns>The lowercase hyphenated interaction key.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The direction is unknown.</exception>
    public static string GetSitWatchInteractionKey(MyJiuMiSitWatchDirection direction) => direction switch
    {
        MyJiuMiSitWatchDirection.Left => SitWatchLeftInteractionKey,
        MyJiuMiSitWatchDirection.Center => SitWatchCenterInteractionKey,
        MyJiuMiSitWatchDirection.Right => SitWatchRightInteractionKey,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown sit-watch direction."),
    };

    /// <summary>
    /// Gets the canonical manifest clip key for a <see cref="MyJiuMiState"/>.
    /// </summary>
    /// <param name="state">The animation state.</param>
    /// <returns>The lowercase hyphenated clip key.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The state has no canonical clip key.</exception>
    public static string GetClipKey(MyJiuMiState state) => state switch
    {
        MyJiuMiState.LyingIdle => "lying-idle",
        MyJiuMiState.Blink => "blink",
        MyJiuMiState.React => "react",
        MyJiuMiState.Drag => "drag",
        MyJiuMiState.SleepIn => "sleep-in",
        MyJiuMiState.Sleep => "sleep",
        MyJiuMiState.Wake => "wake",
        MyJiuMiState.Stretch => "stretch",
        MyJiuMiState.Walk => "walk",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "No My JiuMi clip key exists."),
    };

    /// <summary>
    /// Gets the clip for a core <see cref="MyJiuMiState"/>.
    /// </summary>
    /// <param name="state">The animation state.</param>
    /// <returns>The matching manifest clip.</returns>
    /// <exception cref="KeyNotFoundException">The clip is missing from <see cref="Clips"/>.</exception>
    public MyJiuMiPetPackClip GetClip(MyJiuMiState state)
    {
        string key = GetClipKey(state);
        return Clips.TryGetValue(key, out MyJiuMiPetPackClip? clip)
            ? clip
            : throw new KeyNotFoundException($"The character pack manifest is missing clip '{key}'.");
    }

    /// <summary>
    /// Attempts to retrieve the optional <c>jump</c> clip used by the click jump overlay.
    /// </summary>
    /// <param name="clip">The jump clip when present.</param>
    /// <returns><see langword="true"/> when the manifest declares a <c>jump</c> clip.</returns>
    public bool TryGetJumpClip(out MyJiuMiPetPackClip? clip) =>
        Clips.TryGetValue(JumpClipKey, out clip);

    /// <summary>
    /// Attempts to retrieve the optional sit-watch interaction clip for <paramref name="direction"/>.
    /// </summary>
    /// <param name="direction">The sit-watch gaze direction.</param>
    /// <param name="clip">The matching interaction clip when present.</param>
    /// <returns><see langword="true"/> when the interaction slot is declared.</returns>
    public bool TryGetSitWatchClip(MyJiuMiSitWatchDirection direction, out MyJiuMiPetPackClip? clip) =>
        Interactions.TryGetValue(GetSitWatchInteractionKey(direction), out clip);

    /// <summary>
    /// Attempts to retrieve the optional <c>sit-to-lie</c> transition clip from <see cref="Interactions"/>.
    /// </summary>
    /// <param name="clip">The transition clip when present.</param>
    /// <returns><see langword="true"/> when the interaction slot is declared.</returns>
    public bool TryGetSitToLieClip(out MyJiuMiPetPackClip? clip) =>
        Interactions.TryGetValue(SitToLieInteractionKey, out clip);

    /// <summary>
    /// Attempts to resolve the manifest clip backing <paramref name="state"/> and
    /// <paramref name="watchDirection"/>, covering both core clips and sit-watch interaction slots.
    /// </summary>
    /// <param name="state">The current animation state.</param>
    /// <param name="watchDirection">The active gaze direction when <paramref name="state"/> is <see cref="MyJiuMiState.SitWatch"/>.</param>
    /// <param name="clip">The resolved clip when available.</param>
    /// <returns><see langword="true"/> when a matching clip exists in the manifest.</returns>
    public bool TryGetActiveClip(
        MyJiuMiState state,
        MyJiuMiSitWatchDirection watchDirection,
        out MyJiuMiPetPackClip? clip)
    {
        if (state == MyJiuMiState.SitWatch)
        {
            return TryGetSitWatchClip(watchDirection, out clip);
        }

        if (state == MyJiuMiState.SitToLie)
        {
            return TryGetSitToLieClip(out clip);
        }

        return Clips.TryGetValue(GetClipKey(state), out clip);
    }
}
