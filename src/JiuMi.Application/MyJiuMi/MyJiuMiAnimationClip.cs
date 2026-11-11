namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The exported frames of one animation state.
/// </summary>
/// <param name="AssetFolder">The folder below <c>Assets/MyJiuMi</c> that holds the PNG frames.</param>
/// <param name="FrameDurations">The display time of each frame, in playback order.</param>
/// <param name="Loops">Whether the last frame returns to the first frame.</param>
public sealed record MyJiuMiAnimationClip(
    string AssetFolder,
    IReadOnlyList<TimeSpan> FrameDurations,
    bool Loops)
{
    /// <summary>
    /// Gets the time one full pass through the frames takes.
    /// </summary>
    public TimeSpan Duration
    {
        get
        {
            TimeSpan total = TimeSpan.Zero;
            foreach (TimeSpan duration in FrameDurations)
            {
                total += duration;
            }

            return total;
        }
    }
}
