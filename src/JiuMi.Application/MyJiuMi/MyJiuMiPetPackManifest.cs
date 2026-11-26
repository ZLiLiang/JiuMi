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
}
