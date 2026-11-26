namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// One animation clip declared in a character pack manifest.
/// </summary>
/// <param name="Key">The canonical clip key (for example <c>lying-idle</c> or <c>jump</c>).</param>
/// <param name="Frames">The ordered frames of the clip.</param>
/// <param name="Loops">Whether playback wraps from the last frame back to the first.</param>
/// <param name="DefaultAnchor">The effective clip-level anchor before per-frame overrides.</param>
public sealed record MyJiuMiPetPackClip(
    string Key,
    IReadOnlyList<MyJiuMiPetPackFrame> Frames,
    bool Loops,
    MyJiuMiPetPackAnchor DefaultAnchor)
{
    /// <summary>
    /// Gets the total duration of one pass through <see cref="Frames"/>.
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            TimeSpan total = TimeSpan.Zero;
            foreach (MyJiuMiPetPackFrame frame in Frames)
            {
                total += frame.Duration;
            }

            return total;
        }
    }

    /// <summary>
    /// Converts this manifest clip into a timing-only <see cref="MyJiuMiAnimationClip"/>.
    /// </summary>
    /// <returns>The equivalent animation clip.</returns>
    public MyJiuMiAnimationClip ToAnimationClip()
    {
        TimeSpan[] durations = new TimeSpan[Frames.Count];
        for (int index = 0; index < Frames.Count; index++)
        {
            durations[index] = Frames[index].Duration;
        }

        return new MyJiuMiAnimationClip(Key, durations, Loops);
    }
}
