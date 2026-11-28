namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The accepted micro-motion values: breathing, whole-body sway, sit-watch micro-tilt and the standing jump.
/// </summary>
/// <remarks>
/// The values come from the interactive prototype accepted for #148 and the sit-watch extension in #221.
/// The page host samples this type on every tick; the desktop host samples breathing and sit-watch
/// micro-tilt when the character pack declares sit-watch slots, keeping legacy packs static.
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
    /// Gets the maximum micro-tilt angle, in degrees, applied during <see cref="MyJiuMiState.SitWatch"/>
    /// in response to the cursor's vertical offset from the cat's head.
    /// </summary>
    public const double SitWatchMaxTiltDegrees = 3.0;

    /// <summary>
    /// Gets the vertical cursor offset, in DIP, at which the sit-watch micro-tilt reaches
    /// <see cref="SitWatchMaxTiltDegrees"/>.
    /// </summary>
    public const double SitWatchTiltRangeDip = 200.0;

    /// <summary>
    /// Gets the exponential smoothing time constant, in seconds, used to blend sit-watch micro-tilt
    /// transitions without snapping.
    /// </summary>
    public const double SitWatchTiltSmoothingSeconds = 0.08;

    /// <summary>
    /// Gets the maximum elapsed step, in seconds, fed into sit-watch tilt smoothing on a single tick.
    /// </summary>
    public const double SitWatchTiltMaxStepSeconds = 0.25;

    /// <summary>
    /// Gets the angle deadband, in degrees, below which a returning tilt snaps cleanly to zero.
    /// </summary>
    public const double SitWatchTiltDeadbandDegrees = 0.01;

    /// <summary>
    /// Gets the jump height in DIP for the current cat slot.
    /// </summary>
    public static double JumpHeightDip => MyJiuMiInteractionRules.CatSize * JumpHeightRatio;

    /// <summary>
    /// Advances the smoothed sit-watch micro-tilt toward <paramref name="targetDegrees"/> using
    /// exponential smoothing over <paramref name="elapsed"/>.
    /// </summary>
    /// <param name="currentDegrees">The previous smoothed angle in degrees.</param>
    /// <param name="targetDegrees">The desired target angle in degrees.</param>
    /// <param name="elapsed">The time elapsed since the last motion sample.</param>
    /// <returns>The updated smoothed angle, clamped to <c>±3°</c>.</returns>
    public static double SmoothSitWatchTiltDegrees(
        double currentDegrees,
        double targetDegrees,
        TimeSpan elapsed)
    {
        double current = double.IsFinite(currentDegrees)
            ? Math.Clamp(currentDegrees, -SitWatchMaxTiltDegrees, SitWatchMaxTiltDegrees)
            : 0;
        double target = double.IsFinite(targetDegrees)
            ? Math.Clamp(targetDegrees, -SitWatchMaxTiltDegrees, SitWatchMaxTiltDegrees)
            : 0;

        double dt = Math.Clamp(elapsed.TotalSeconds, 0, SitWatchTiltMaxStepSeconds);
        double alpha = dt > 0 ? 1 - Math.Exp(-dt / SitWatchTiltSmoothingSeconds) : 1;
        double next = current + ((target - current) * alpha);
        if (Math.Abs(next) < SitWatchTiltDeadbandDegrees &&
            Math.Abs(target) < SitWatchTiltDeadbandDegrees)
        {
            return 0;
        }

        return Math.Clamp(next, -SitWatchMaxTiltDegrees, SitWatchMaxTiltDegrees);
    }

    /// <summary>
    /// Computes the gentle <c>±3°</c> tilt applied during <see cref="MyJiuMiState.SitWatch"/> from
    /// the cursor's vertical offset relative to the cat's head.
    /// </summary>
    /// <param name="direction">The current horizontal gaze direction.</param>
    /// <param name="cursorOffsetYDip">The cursor Y minus the cat head Y, in DIP (negative is above).</param>
    /// <returns>The target rotation in degrees, clamped to <c>±3°</c>.</returns>
    public static double ComputeSitWatchTiltDegrees(
        MyJiuMiSitWatchDirection direction,
        double cursorOffsetYDip)
    {
        if (!double.IsFinite(cursorOffsetYDip))
        {
            return 0;
        }

        double normalizedY = Math.Clamp(cursorOffsetYDip / SitWatchTiltRangeDip, -1, 1);
        double sign = direction == MyJiuMiSitWatchDirection.Left ? -1 : 1;
        return normalizedY * SitWatchMaxTiltDegrees * sign;
    }

    /// <summary>
    /// Samples the resting breath and sway for an animation state.
    /// </summary>
    /// <param name="elapsed">The time since the page visit started.</param>
    /// <param name="state">The animation state the cat is in.</param>
    /// <returns>The transform and shadow for this instant.</returns>
    public static MyJiuMiMotion Sample(TimeSpan elapsed, MyJiuMiState state) =>
        Sample(elapsed, state, sitWatchTiltDegrees: null);

    /// <summary>
    /// Samples the resting breath, sway or sit-watch micro-tilt for an animation state.
    /// </summary>
    /// <param name="elapsed">The time since the session motion started.</param>
    /// <param name="state">The animation state the cat is in.</param>
    /// <param name="sitWatchTiltDegrees">Optional micro-tilt angle applied during <see cref="MyJiuMiState.SitWatch"/>.</param>
    /// <returns>The transform and shadow for this instant.</returns>
    public static MyJiuMiMotion Sample(
        TimeSpan elapsed,
        MyJiuMiState state,
        double? sitWatchTiltDegrees)
    {
        double seconds = Math.Max(0, elapsed.TotalSeconds);
        bool sleeping = state is MyJiuMiState.SleepIn or MyJiuMiState.Sleep;
        double period = BreathPeriod.TotalSeconds * (sleeping ? SleepBreathPeriodScale : 1);
        double amplitude = BreathAmplitude * (sleeping ? SleepBreathAmplitudeScale : 1);
        double breath = Math.Sin(seconds / period * TwoPi);
        double rotation;
        if (state == MyJiuMiState.SitWatch &&
            sitWatchTiltDegrees is { } tilt &&
            double.IsFinite(tilt))
        {
            rotation = Math.Clamp(tilt, -SitWatchMaxTiltDegrees, SitWatchMaxTiltDegrees);
        }
        else if (IsSwaying(state))
        {
            rotation = Math.Sin((seconds / (period * SwayPeriodScale) * TwoPi) + SwayPhaseRadians) *
                SwayAmplitudeDegrees;
        }
        else
        {
            rotation = 0;
        }

        return new MyJiuMiMotion(
            ScaleX: 1 - (amplitude * BreathWidthRatio * breath),
            ScaleY: 1 + (amplitude * breath),
            RotationDegrees: rotation,
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
