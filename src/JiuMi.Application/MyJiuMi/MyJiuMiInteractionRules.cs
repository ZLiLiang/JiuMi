namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The timing, movement and overlay constants of the My JiuMi page.
/// </summary>
public static class MyJiuMiInteractionRules
{
    /// <summary>
    /// Gets how long the cat stays awake without interaction before curling down to sleep.
    /// </summary>
    public static TimeSpan SleepDelay { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the shortest random delay between idle blinks.
    /// </summary>
    public static TimeSpan BlinkDelayMin { get; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the longest random delay between idle blinks.
    /// </summary>
    public static TimeSpan BlinkDelayMax { get; } = TimeSpan.FromSeconds(7);

    /// <summary>
    /// Gets the shortest random pause between autonomous wander decisions.
    /// </summary>
    public static TimeSpan WanderDelayMin { get; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets the longest random pause between autonomous wander decisions.
    /// </summary>
    public static TimeSpan WanderDelayMax { get; } = TimeSpan.FromSeconds(40);

    /// <summary>
    /// Gets the smallest distance, in DIP, between the cat and a wander target.
    /// </summary>
    public const double WanderMinDistance = 120;

    /// <summary>
    /// Gets the margin, in DIP, a wander target keeps from the room edges.
    /// </summary>
    public const double WanderEdgeMargin = 40;

    /// <summary>
    /// Gets the walking speed in DIP per second, matched to the 33-frame walk stride so the paws do
    /// not slide.
    /// </summary>
    public const double WalkSpeed = 103;

    /// <summary>
    /// Gets the walking speed in DIP per second used while the desktop cat walks home and the page
    /// cat finishes the summon; 1.6x <see cref="WalkSpeed"/> keeps the stride of the fast frames.
    /// </summary>
    public const double SummonWalkSpeed = 167;

    /// <summary>
    /// Gets the frame interval of the summon walk, matched to <see cref="SummonWalkSpeed"/>.
    /// </summary>
    public const int SummonWalkFrameMilliseconds = 16;

    /// <summary>
    /// Gets the side of the square cat slot, matching the 200 DIP display size.
    /// </summary>
    public const double CatSize = 200;

    /// <summary>
    /// Gets the width, in DIP, of the cat's pointer hit area; the exported cat art is about
    /// 170–177 DIP wide, so this keeps empty floor on either side clickable.
    /// </summary>
    public const double CatHitWidth = 180;

    /// <summary>
    /// Gets the height, in DIP, of the cat's pointer hit area above the floor line; the tallest
    /// exported frame has visible art within the bottom 121 DIP.
    /// </summary>
    public const double CatHitHeight = 140;

    /// <summary>
    /// Gets the horizontal pointer movement, in pixels, that turns a click into a drag.
    /// </summary>
    public const double DragThreshold = 4;

    /// <summary>
    /// Gets how long the heart waits after a click before it appears.
    /// </summary>
    public static TimeSpan HeartDelay { get; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Gets how long the heart overlay stays visible.
    /// </summary>
    public static TimeSpan HeartDuration { get; } = TimeSpan.FromMilliseconds(800);

    /// <summary>
    /// Gets the centre deadzone radius from the cat's head centre, in DIP, within which the cat looks
    /// forward in <see cref="MyJiuMiState.SitWatch"/>.
    /// </summary>
    public const double SitWatchCenterRadiusDip = 60;

    /// <summary>
    /// Gets the radial hysteresis half-band, in DIP, applied around <see cref="SitWatchCenterRadiusDip"/>
    /// to prevent rapid switching when the cursor hovers near the centre deadzone boundary.
    /// </summary>
    public const double SitWatchRadiusHysteresisDip = 15;

    /// <summary>
    /// Gets the angular hysteresis half-band, in degrees, applied across directional sector boundaries
    /// in <see cref="MyJiuMiState.SitWatch"/> to prevent flicker when hovering near a seam.
    /// </summary>
    public const double SitWatchAngleHysteresisDegrees = 7.5;

    /// <summary>
    /// Gets the minimum elapsed time between gaze direction changes in <see cref="MyJiuMiState.SitWatch"/>.
    /// </summary>
    public static TimeSpan SitWatchMinSwitchInterval { get; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets the cursor displacement, in DIP, that counts as an effective cursor movement and resets
    /// the sit-watch stillness timeout.
    /// </summary>
    public const double SitWatchEffectiveMoveDip = 8;

    /// <summary>
    /// Gets the maximum continuous time the cat keeps watching the cursor before getting tired.
    /// </summary>
    public static TimeSpan SitWatchMaxDuration { get; } = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Gets how long the cursor may remain without effective movement before the sitting cat loses interest.
    /// </summary>
    public static TimeSpan SitWatchIdleTimeout { get; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets how long the cat rests in <see cref="MyJiuMiState.LyingIdle"/> after lying down from
    /// <see cref="MyJiuMiState.SitWatch"/> before entering <see cref="MyJiuMiState.SleepIn"/>.
    /// </summary>
    public static TimeSpan SitWatchLieSleepBuffer { get; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets the probability that a page-mode cat lies down to sleep (rather than wandering) when
    /// sit-watch patience runs out.
    /// </summary>
    public const double SitWatchPageSleepProbability = 0.75;

    /// <summary>
    /// Gets the vertical position of the sitting cat's head centre measured upward from the floor
    /// anchor as a fraction of the square cat slot.
    /// </summary>
    public const double SitWatchHeadHeightRatio = 0.6;

    /// <summary>
    /// Projects <paramref name="direction"/> to the nearest sit-watch direction supported by
    /// <paramref name="tier"/> along the degradation ladder (9-way → 5-way → 3-way → centre).
    /// </summary>
    /// <param name="direction">The desired sit-watch gaze direction.</param>
    /// <param name="tier">The sit-watch directional capability tier supported by the active pack.</param>
    /// <returns>The degraded gaze direction supported by <paramref name="tier"/>.</returns>
    public static MyJiuMiSitWatchDirection DegradeSitWatchDirection(
        MyJiuMiSitWatchDirection direction,
        MyJiuMiSitWatchTier tier) => tier switch
        {
            MyJiuMiSitWatchTier.NineWay => direction,
            MyJiuMiSitWatchTier.FiveWay => direction switch
            {
                MyJiuMiSitWatchDirection.UpLeft or MyJiuMiSitWatchDirection.DownLeft => MyJiuMiSitWatchDirection.Left,
                MyJiuMiSitWatchDirection.UpRight or MyJiuMiSitWatchDirection.DownRight => MyJiuMiSitWatchDirection.Right,
                _ => direction,
            },
            MyJiuMiSitWatchTier.ThreeWay => direction switch
            {
                MyJiuMiSitWatchDirection.Left
                    or MyJiuMiSitWatchDirection.UpLeft
                    or MyJiuMiSitWatchDirection.DownLeft => MyJiuMiSitWatchDirection.Left,
                MyJiuMiSitWatchDirection.Right
                    or MyJiuMiSitWatchDirection.UpRight
                    or MyJiuMiSitWatchDirection.DownRight => MyJiuMiSitWatchDirection.Right,
                _ => MyJiuMiSitWatchDirection.Center,
            },
            _ => MyJiuMiSitWatchDirection.Center,
        };

    /// <summary>
    /// Classifies an initial 2D cursor offset relative to the cat's head centre in polar coordinates
    /// (centre deadzone radius plus 360-degree directional sectors degraded to <paramref name="tier"/>).
    /// </summary>
    /// <param name="offsetXDip">The cursor X minus the cat head centre X, in DIP.</param>
    /// <param name="offsetYDip">The cursor Y minus the cat head centre Y, in DIP (negative is above).</param>
    /// <param name="tier">The sit-watch directional capability tier supported by the active pack.</param>
    /// <returns>The matching gaze direction supported by <paramref name="tier"/>.</returns>
    public static MyJiuMiSitWatchDirection ClassifySitWatchDirection(
        double offsetXDip,
        double offsetYDip,
        MyJiuMiSitWatchTier tier = MyJiuMiSitWatchTier.NineWay)
    {
        if (!double.IsFinite(offsetXDip) ||
            !double.IsFinite(offsetYDip) ||
            tier == MyJiuMiSitWatchTier.None)
        {
            return MyJiuMiSitWatchDirection.Center;
        }

        double radius = double.Hypot(offsetXDip, offsetYDip);
        if (radius <= SitWatchCenterRadiusDip)
        {
            return MyJiuMiSitWatchDirection.Center;
        }

        return ClassifyOuterDirection(ToAngleDegrees(offsetXDip, offsetYDip), tier);
    }

    /// <summary>
    /// Resolves the next 360-degree gaze direction from <paramref name="current"/> by applying radial
    /// hysteresis around <see cref="SitWatchCenterRadiusDip"/> and angular hysteresis across sector
    /// boundaries for <paramref name="tier"/>.
    /// </summary>
    /// <param name="current">The currently active gaze direction.</param>
    /// <param name="offsetXDip">The cursor X minus the cat head centre X, in DIP.</param>
    /// <param name="offsetYDip">The cursor Y minus the cat head centre Y, in DIP (negative is above).</param>
    /// <param name="tier">The sit-watch directional capability tier supported by the active pack.</param>
    /// <returns>The updated gaze direction supported by <paramref name="tier"/>.</returns>
    public static MyJiuMiSitWatchDirection ResolveSitWatchDirection(
        MyJiuMiSitWatchDirection current,
        double offsetXDip,
        double offsetYDip,
        MyJiuMiSitWatchTier tier = MyJiuMiSitWatchTier.NineWay)
    {
        if (!double.IsFinite(offsetXDip) || !double.IsFinite(offsetYDip))
        {
            return current;
        }

        if (tier is not (MyJiuMiSitWatchTier.NineWay or MyJiuMiSitWatchTier.FiveWay or MyJiuMiSitWatchTier.ThreeWay))
        {
            return MyJiuMiSitWatchDirection.Center;
        }

        MyJiuMiSitWatchDirection normalizedCurrent = DegradeSitWatchDirection(current, tier);
        double radius = double.Hypot(offsetXDip, offsetYDip);
        double exitCenterRadius = SitWatchCenterRadiusDip + SitWatchRadiusHysteresisDip;
        double enterCenterRadius = SitWatchCenterRadiusDip - SitWatchRadiusHysteresisDip;

        if (normalizedCurrent == MyJiuMiSitWatchDirection.Center)
        {
            if (radius <= exitCenterRadius)
            {
                return MyJiuMiSitWatchDirection.Center;
            }

            double angleDegrees = ToAngleDegrees(offsetXDip, offsetYDip);
            if (tier == MyJiuMiSitWatchTier.ThreeWay)
            {
                double verticalHoldHalfAngle = 22.5 + SitWatchAngleHysteresisDegrees;
                if (AngularDistanceDegrees(angleDegrees, -90.0) <= verticalHoldHalfAngle ||
                    AngularDistanceDegrees(angleDegrees, 90.0) <= verticalHoldHalfAngle)
                {
                    return MyJiuMiSitWatchDirection.Center;
                }

                return offsetXDip < 0 ? MyJiuMiSitWatchDirection.Left : MyJiuMiSitWatchDirection.Right;
            }

            return ClassifyOuterDirection(angleDegrees, tier);
        }

        if (radius <= enterCenterRadius)
        {
            return MyJiuMiSitWatchDirection.Center;
        }

        double outerAngleDegrees = ToAngleDegrees(offsetXDip, offsetYDip);
        double halfSector = tier switch
        {
            MyJiuMiSitWatchTier.NineWay => 22.5,
            MyJiuMiSitWatchTier.FiveWay => 45.0,
            _ => 67.5,
        };
        if (AngularDistanceDegrees(outerAngleDegrees, GetDirectionAngleDegrees(normalizedCurrent)) <=
            halfSector + SitWatchAngleHysteresisDegrees)
        {
            return normalizedCurrent;
        }

        return ClassifyOuterDirection(outerAngleDegrees, tier);
    }

    private static MyJiuMiSitWatchDirection ClassifyOuterDirection(
        double angleDegrees,
        MyJiuMiSitWatchTier tier) => tier switch
        {
            MyJiuMiSitWatchTier.NineWay => ClassifyOuterEightWay(angleDegrees),
            MyJiuMiSitWatchTier.FiveWay => ClassifyOuterFiveWay(angleDegrees),
            MyJiuMiSitWatchTier.ThreeWay => DegradeSitWatchDirection(
                ClassifyOuterEightWay(angleDegrees),
                MyJiuMiSitWatchTier.ThreeWay),
            _ => MyJiuMiSitWatchDirection.Center,
        };

    private static MyJiuMiSitWatchDirection ClassifyOuterEightWay(double angleDegrees) =>
        ToSectorIndex(angleDegrees, sectorCount: 8) switch
        {
            0 => MyJiuMiSitWatchDirection.Right,
            1 => MyJiuMiSitWatchDirection.DownRight,
            2 => MyJiuMiSitWatchDirection.Down,
            3 => MyJiuMiSitWatchDirection.DownLeft,
            4 => MyJiuMiSitWatchDirection.Left,
            5 => MyJiuMiSitWatchDirection.UpLeft,
            6 => MyJiuMiSitWatchDirection.Up,
            _ => MyJiuMiSitWatchDirection.UpRight,
        };

    private static MyJiuMiSitWatchDirection ClassifyOuterFiveWay(double angleDegrees) =>
        ToSectorIndex(angleDegrees, sectorCount: 4) switch
        {
            0 => MyJiuMiSitWatchDirection.Right,
            1 => MyJiuMiSitWatchDirection.Down,
            2 => MyJiuMiSitWatchDirection.Left,
            _ => MyJiuMiSitWatchDirection.Up,
        };

    private static double ToAngleDegrees(double offsetXDip, double offsetYDip) =>
        Math.Atan2(offsetYDip, offsetXDip) * (180.0 / Math.PI);

    private static int ToSectorIndex(double angleDegrees, int sectorCount)
    {
        double sectorWidth = 360.0 / sectorCount;
        double normalized = ((angleDegrees % 360.0) + 360.0) % 360.0;
        return (int)Math.Floor(((normalized + (sectorWidth / 2.0)) % 360.0) / sectorWidth);
    }

    private static double GetDirectionAngleDegrees(MyJiuMiSitWatchDirection direction) => direction switch
    {
        MyJiuMiSitWatchDirection.Right => 0.0,
        MyJiuMiSitWatchDirection.DownRight => 45.0,
        MyJiuMiSitWatchDirection.Down => 90.0,
        MyJiuMiSitWatchDirection.DownLeft => 135.0,
        MyJiuMiSitWatchDirection.Left => 180.0,
        MyJiuMiSitWatchDirection.UpLeft => -135.0,
        MyJiuMiSitWatchDirection.Up => -90.0,
        MyJiuMiSitWatchDirection.UpRight => -45.0,
        _ => 0.0,
    };

    private static double AngularDistanceDegrees(double a, double b)
    {
        double difference = Math.Abs(a - b) % 360.0;
        return difference > 180.0 ? 360.0 - difference : difference;
    }
}
