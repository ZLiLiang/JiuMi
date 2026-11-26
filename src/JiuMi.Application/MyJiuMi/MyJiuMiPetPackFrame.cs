namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// One animation frame declared in a character pack manifest, with its resolved anchor.
/// </summary>
/// <param name="Path">The pack-relative forward-slash path to the PNG frame.</param>
/// <param name="Duration">How long the frame stays on screen.</param>
/// <param name="Anchor">The effective canvas anchor after applying frame and clip overrides.</param>
public sealed record MyJiuMiPetPackFrame(
    string Path,
    TimeSpan Duration,
    MyJiuMiPetPackAnchor Anchor);
