namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The accepted micro-motion values: breathing, whole-body sway, blinking and the standing jump.
/// </summary>
/// <remarks>
/// The values come from the interactive prototype accepted for #148. The page host samples this type
/// on every tick of the shared session; the desktop host keeps its existing static rendering.
/// </remarks>
public static class MyJiuMiMotionRules
{
    /// <summary>
    /// Gets the breathing amplitude as a fraction of the body height.
    /// </summary>
    public const double BreathAmplitude = 0.015;

    /// <summary>
    /// Gets how much of the breathing amplitude shrinks the body width instead.
    /// </summary>
    public const double BreathWidthRatio = 0.35;

    /// <summary>
    /// Gets the shortest time the cat stays standing.
    /// </summary>
    public static TimeSpan JumpDuration { get; } = TimeSpan.FromSeconds(0.9);

    /// <summary>
    /// Gets the frame index inside the stretch clip that shows the standing pose.
    /// </summary>
    public const int JumpFrameIndex = 3;

    /// <summary>
    /// Gets the jump height as a fraction of the cat slot.
    /// </summary>
    public const double JumpHeightRatio = 0.188;

    private const double TwoPi = Math.PI * 2;
    private const double BreathShadowScale = 0.08;
    private const double JumpShadowMinOpacity = 0.45;

    /// <summary>
    /// Gets the breathing period of the awake cat.
    /// </summary>
    public static TimeSpan BreathPeriod { get; } = TimeSpan.FromSeconds(3.6);

    /// <summary>
    /// Gets how much longer the sleeping breath cycle is.
    /// </summary>
    public const double SleepBreathPeriodScale = 1.5;

    /// <summary>
    /// Gets how much shallower the sleeping breath is.
    /// </summary>
    public const double SleepBreathAmplitudeScale = 0.8;

    /// <summary>
    /// Gets the sway amplitude in degrees around the bottom centre.
    /// </summary>
    public const double SwayAmplitudeDegrees = 0.6;

    /// <summary>
    /// Gets the sway period as a multiple of the breathing period.
    /// </summary>
    public const double SwayPeriodScale = 1.7;

    /// <summary>
    /// Gets the sway phase offset in radians, so the sway does not peak with the breath.
    /// </summary>
    public const double SwayPhaseRadians = 0.8;

    /// <summary>
    /// Gets the jump height in DIP for the current cat slot.
    /// </summary>
    public static double JumpHeightDip => MyJiuMiInteractionRules.CatSize * JumpHeightRatio;

    /// <summary>
    /// Samples the resting breath and sway for an animation state.
    /// </summary>
    /// <param name="elapsed">The time since the page visit started.</param>
    /// <param name="state">The animation state the cat is in.</param>
    /// <returns>The transform and shadow for this instant.</returns>
    public static MyJiuMiMotion Sample(TimeSpan elapsed, MyJiuMiState state)
    {
        double seconds = Math.Max(0, elapsed.TotalSeconds);
        bool sleeping = state is MyJiuMiState.SleepIn or MyJiuMiState.Sleep;
        double period = BreathPeriod.TotalSeconds * (sleeping ? SleepBreathPeriodScale : 1);
        double amplitude = BreathAmplitude * (sleeping ? SleepBreathAmplitudeScale : 1);
        double breath = Math.Sin(seconds / period * TwoPi);
        double sway = IsSwaying(state)
            ? Math.Sin((seconds / (period * SwayPeriodScale) * TwoPi) + SwayPhaseRadians) *
                SwayAmplitudeDegrees
            : 0;

        return new MyJiuMiMotion(
            ScaleX: 1 - (amplitude * BreathWidthRatio * breath),
            ScaleY: 1 + (amplitude * breath),
            RotationDegrees: sway,
            OffsetY: 0,
            ShadowScale: 1 - (BreathShadowScale * Math.Max(0, breath)),
            ShadowOpacity: 1);
    }

    /// <summary>
    /// Samples the standing jump: squash, rise, fall, landing squash and rebound.
    /// </summary>
    /// <param name="elapsed">The time since the jump started.</param>
    /// <returns>The transform and floor shadow for this instant.</returns>
    public static MyJiuMiMotion SampleJump(TimeSpan elapsed)
    {
        double duration = JumpDuration.TotalSeconds;
        double progress = duration > 0
            ? Math.Clamp(elapsed.TotalSeconds / duration, 0, 1)
            : 1;
        double height = JumpHeightDip;
        double offset = 0;
        double scaleX = 1;
        double scaleY = 1;
        double shadowScale = 1;

        if (progress < 0.14)
        {
            double p = progress / 0.14;
            scaleY = 1 - (0.13 * p);
            scaleX = 1 + (0.08 * p);
        }
        else if (progress < 0.44)
        {
            double p = EaseOut((progress - 0.14) / 0.30);
            offset = -height * p;
            scaleY = 1.04;
            scaleX = 0.97;
            shadowScale = 1 - (0.38 * p);
        }
        else if (progress < 0.70)
        {
            double p = (progress - 0.44) / 0.26;
            offset = -height * (1 - p);
            scaleY = 1.02;
            scaleX = 0.99;
            shadowScale = 1 - (0.38 * (1 - p));
        }
        else if (progress < 0.84)
        {
            double p = (progress - 0.70) / 0.14;
            double squash = Math.Sin(p * Math.PI);
            scaleY = 1 - (0.10 * squash);
            scaleX = 1 + (0.07 * squash);
            shadowScale = 1 + (0.08 * squash);
        }
        else
        {
            double p = (progress - 0.84) / 0.16;
            scaleY = 1 - (0.10 * (1 - p));
            scaleX = 1 + (0.07 * (1 - p));
            shadowScale = 1 + (0.08 * (1 - p));
        }

        double lift = height > 0 ? Math.Abs(offset) / height : 0;
        double opacity = Math.Max(JumpShadowMinOpacity, 1 - (0.38 * lift));

        return new MyJiuMiMotion(
            ScaleX: scaleX,
            ScaleY: scaleY,
            RotationDegrees: 0,
            OffsetY: offset,
            ShadowScale: shadowScale,
            ShadowOpacity: opacity);
    }

    private static double EaseOut(double value) => 1 - Math.Pow(1 - value, 3);

    private static bool IsSwaying(MyJiuMiState state) =>
        state is MyJiuMiState.LyingIdle
            or MyJiuMiState.Blink
            or MyJiuMiState.SleepIn
            or MyJiuMiState.Sleep;
}
