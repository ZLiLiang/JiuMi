using System.Diagnostics.CodeAnalysis;

namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// Decides which My JiuMi animation plays, when the cat blinks, wanders, sleeps or wakes, and where
/// it stands on the room floor.
/// </summary>
/// <remarks>
/// The machine owns no timers and draws nothing: the page calls <see cref="Update"/> about every
/// 16 ms and renders <see cref="State"/>, <see cref="FrameIndex"/> and <see cref="PositionX"/>.
/// Time and randomness are injected so the transitions are unit-testable without a real clock.
/// </remarks>
public sealed class MyJiuMiStateMachine
{
    private const int MaxTransitionsPerUpdate = 16;

    private readonly TimeProvider _timeProvider;
    private readonly Random _random;

    private MyJiuMiState _state;
    private DateTimeOffset _stateStartedAt;
    private DateTimeOffset _lastUpdateAt;
    private DateTimeOffset _sleepDeadline;
    private DateTimeOffset? _nextBlinkAt;
    private DateTimeOffset? _nextWanderAt;
    private int _frameIndex;
    private double _positionX = MyJiuMiPosition.Default;
    private double _stageWidth;
    private double _walkTargetX;
    private bool _isFacingRight;
    private bool _isActive;
    private PendingAction _pendingAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyJiuMiStateMachine"/> class. The cat stays
    /// inactive until <see cref="Activate"/>.
    /// </summary>
    /// <param name="timeProvider">Supplies the current time.</param>
    /// <param name="random">Picks blink and wander delays, directions and targets.</param>
    public MyJiuMiStateMachine(TimeProvider timeProvider, Random random)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(random);

        _timeProvider = timeProvider;
        _random = random;
    }

    /// <summary>
    /// Gets a value indicating whether the page is showing the cat.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets the animation currently playing.
    /// </summary>
    public MyJiuMiState State => _state;

    /// <summary>
    /// Gets the zero-based frame of <see cref="State"/> to draw.
    /// </summary>
    public int FrameIndex => _frameIndex;

    /// <summary>
    /// Gets the cat's horizontal position as a fraction of the room width.
    /// </summary>
    public double PositionX => _positionX;

    /// <summary>
    /// Gets a value indicating whether the walking frames should be mirrored to face right.
    /// </summary>
    public bool IsFacingRight => _isFacingRight;

    /// <summary>
    /// Starts a fresh page visit in <see cref="MyJiuMiState.LyingIdle"/> at the saved position.
    /// </summary>
    /// <param name="stageWidth">The room width in DIP.</param>
    /// <param name="positionX">The saved normalized horizontal position.</param>
    public void Activate(double stageWidth, double positionX)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _stageWidth = NormalizeWidth(stageWidth);
        _positionX = MyJiuMiPosition.Clamp(positionX);
        _isFacingRight = false;
        _isActive = true;
        _pendingAction = PendingAction.None;
        _lastUpdateAt = now;
        ResetSleep(now);
        EnterLyingIdle(now, restartWander: true);
    }

    /// <summary>
    /// Stops timers and walking when the page is left; the next <see cref="Activate"/> starts over.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
        _pendingAction = PendingAction.None;
        _nextBlinkAt = null;
        _nextWanderAt = null;
        _state = MyJiuMiState.LyingIdle;
        _frameIndex = 0;
    }

    /// <summary>
    /// Updates the room width after a window resize. The stored normalized position is unchanged.
    /// </summary>
    /// <param name="stageWidth">The new room width in DIP.</param>
    public void SetStageWidth(double stageWidth) => _stageWidth = NormalizeWidth(stageWidth);

    /// <summary>
    /// Moves the cat while the pointer drags it.
    /// </summary>
    /// <param name="positionX">The normalized pointer position.</param>
    public void SetPosition(double positionX)
    {
        if (_isActive)
        {
            _positionX = MyJiuMiPosition.Clamp(positionX);
        }
    }

    /// <summary>
    /// Advances the animation to the current time: changes frames, wanders, blinks and falls asleep.
    /// </summary>
    public void Update()
    {
        if (!_isActive)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        AdvanceWalk(now);

        for (int transitions = 0; transitions < MaxTransitionsPerUpdate; transitions++)
        {
            if (IsSleepDue(now) && _state is MyJiuMiState.LyingIdle or MyJiuMiState.Blink)
            {
                _pendingAction = PendingAction.None;
                EnterState(MyJiuMiState.SleepIn, now);
                continue;
            }

            if (_state == MyJiuMiState.LyingIdle)
            {
                if (_nextBlinkAt is { } blinkAt && now >= blinkAt)
                {
                    EnterState(MyJiuMiState.Blink, now);
                    continue;
                }

                if (_nextWanderAt is { } wanderAt && now >= wanderAt)
                {
                    _nextWanderAt = null;
                    if (!TryStartWander(now))
                    {
                        ScheduleWander(now);
                    }

                    continue;
                }

                break;
            }

            if (!TryCompleteTransition(now))
            {
                break;
            }
        }

        UpdateFrame(now);
        _lastUpdateAt = now;
    }

    /// <summary>
    /// Applies a single click on the cat.
    /// </summary>
    /// <returns>Whether the cat reacted with a heart, woke up, or ignored the click.</returns>
    public MyJiuMiClickEffect Click()
    {
        if (!_isActive || _state == MyJiuMiState.Drag)
        {
            return MyJiuMiClickEffect.None;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ResetSleep(now);
        _pendingAction = PendingAction.None;

        if (_state == MyJiuMiState.Sleep)
        {
            EnterState(MyJiuMiState.Wake, now);
            return MyJiuMiClickEffect.Wake;
        }

        EnterState(MyJiuMiState.React, now);
        return MyJiuMiClickEffect.React;
    }

    /// <summary>
    /// Starts the dangling drag loop; a sleeping cat is picked up directly. The drag restarts the
    /// sleep countdown, and a countdown that expires while dragging waits for the release.
    /// </summary>
    public void BeginDrag()
    {
        if (!_isActive)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ResetSleep(now);
        _pendingAction = PendingAction.None;
        EnterState(MyJiuMiState.Drag, now);
    }

    /// <summary>
    /// Ends the drag, returning to the floor. The sleep state is entered at once when the countdown
    /// expired while the cat was picked up.
    /// </summary>
    public void EndDrag()
    {
        if (!_isActive || _state != MyJiuMiState.Drag)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        EnterLyingIdle(now, restartWander: true);
        if (IsSleepDue(now))
        {
            EnterState(MyJiuMiState.SleepIn, now);
        }
    }

    /// <summary>
    /// Applies a double click on the empty floor: the cat wakes when asleep, stretches, walks to the
    /// clicked position and rests there.
    /// </summary>
    /// <param name="targetX">The clicked normalized horizontal position.</param>
    public void Summon(double targetX)
    {
        if (!_isActive || _state == MyJiuMiState.Drag)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ResetSleep(now);
        _walkTargetX = MyJiuMiPosition.Clamp(targetX);
        EnterState(
            _state == MyJiuMiState.Sleep ? MyJiuMiState.Wake : MyJiuMiState.Stretch,
            now);
        _pendingAction = PendingAction.Walk;
    }

    private bool TryStartWander(DateTimeOffset now)
    {
        // The decision is a coin flip; a declined wander simply waits for the next interval.
        if (NextRandom() >= 0.5)
        {
            return false;
        }

        if (!TryChooseWanderTarget(out double target))
        {
            return false;
        }

        _pendingAction = PendingAction.Walk;
        _walkTargetX = target;
        EnterState(MyJiuMiState.Stretch, now);
        return true;
    }

    private bool TryChooseWanderTarget(out double target)
    {
        target = _positionX;
        if (_stageWidth <= 0)
        {
            return false;
        }

        double catHalf = MyJiuMiInteractionRules.CatSize / 2;
        double margin = MyJiuMiInteractionRules.WanderEdgeMargin;
        double minX = (catHalf + margin) / _stageWidth;
        double maxX = 1 - minX;
        if (minX > maxX)
        {
            return false;
        }

        double current = Math.Clamp(_positionX, minX, maxX);
        double minDistance = MyJiuMiInteractionRules.WanderMinDistance / _stageWidth;
        bool goLeft = NextRandom() < 0.5;

        (double directionMin, double directionMax) = goLeft
            ? (minX, current - minDistance)
            : (current + minDistance, maxX);

        if (directionMin > directionMax)
        {
            (directionMin, directionMax) = goLeft
                ? (current + minDistance, maxX)
                : (minX, current - minDistance);
        }

        if (directionMin > directionMax)
        {
            return false;
        }

        target = directionMin + ((directionMax - directionMin) * NextRandom());
        return true;
    }

    private void AdvanceWalk(DateTimeOffset now)
    {
        if (_state != MyJiuMiState.Walk)
        {
            return;
        }

        double remaining = _walkTargetX - _positionX;
        if (Math.Abs(remaining) < double.Epsilon)
        {
            EnterLyingIdle(now, restartWander: true);
            return;
        }

        if (_stageWidth <= 0)
        {
            _positionX = _walkTargetX;
            EnterLyingIdle(now, restartWander: true);
            return;
        }

        double elapsed = Math.Max(0, (now - _lastUpdateAt).TotalSeconds);
        double step = MyJiuMiInteractionRules.WalkSpeed * elapsed / _stageWidth;
        if (step >= Math.Abs(remaining))
        {
            _positionX = _walkTargetX;
            EnterLyingIdle(now, restartWander: true);
            return;
        }

        _positionX += Math.Sign(remaining) * step;
    }

    private bool TryCompleteTransition(DateTimeOffset now)
    {
        MyJiuMiAnimationClip clip = MyJiuMiAnimationCatalog.For(_state);
        if (clip.Loops || now - _stateStartedAt < clip.Duration)
        {
            return false;
        }

        switch (_state)
        {
            case MyJiuMiState.Blink:
                EnterLyingIdle(now, restartWander: false);
                return true;

            case MyJiuMiState.React:
                EnterLyingIdle(now, restartWander: true);
                return true;

            case MyJiuMiState.SleepIn:
                EnterState(MyJiuMiState.Sleep, now);
                return true;

            case MyJiuMiState.Wake:
            case MyJiuMiState.Stretch:
                if (_pendingAction == PendingAction.Walk)
                {
                    StartWalk(now);
                }
                else
                {
                    EnterLyingIdle(now, restartWander: true);
                }

                return true;

            default:
                return false;
        }
    }

    private void StartWalk(DateTimeOffset at)
    {
        _isFacingRight = _walkTargetX > _positionX;
        _pendingAction = PendingAction.None;
        EnterState(MyJiuMiState.Walk, at);
    }

    private void EnterLyingIdle(DateTimeOffset at, bool restartWander)
    {
        EnterState(MyJiuMiState.LyingIdle, at);
        if (restartWander)
        {
            ScheduleWander(at);
        }
    }

    private void EnterState(MyJiuMiState state, DateTimeOffset at)
    {
        _state = state;
        _stateStartedAt = at;
        _frameIndex = 0;

        if (state == MyJiuMiState.LyingIdle)
        {
            _nextBlinkAt = at + NextDelay(
                MyJiuMiInteractionRules.BlinkDelayMin,
                MyJiuMiInteractionRules.BlinkDelayMax);
        }
        else
        {
            _nextBlinkAt = null;
        }

        if (state is not (MyJiuMiState.LyingIdle or MyJiuMiState.Blink))
        {
            _nextWanderAt = null;
        }
    }

    private void ResetSleep(DateTimeOffset now) =>
        _sleepDeadline = now + MyJiuMiInteractionRules.SleepDelay;

    private bool IsSleepDue(DateTimeOffset now) => now >= _sleepDeadline;

    private void ScheduleWander(DateTimeOffset at) =>
        _nextWanderAt = at + NextDelay(
            MyJiuMiInteractionRules.WanderDelayMin,
            MyJiuMiInteractionRules.WanderDelayMax);

    [SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "The delays and directions are cosmetic timing and have no security requirement.")]
    private double NextRandom() => _random.NextDouble();

    private TimeSpan NextDelay(TimeSpan minimum, TimeSpan maximum)
    {
        double fraction = NextRandom();
        return minimum + (maximum - minimum) * fraction;
    }

    private void UpdateFrame(DateTimeOffset now)
    {
        MyJiuMiAnimationClip clip = MyJiuMiAnimationCatalog.For(_state);
        TimeSpan elapsed = now - _stateStartedAt;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (clip.Loops && clip.Duration > TimeSpan.Zero)
        {
            elapsed = TimeSpan.FromTicks(elapsed.Ticks % clip.Duration.Ticks);
        }

        TimeSpan boundary = TimeSpan.Zero;
        for (int index = 0; index < clip.FrameDurations.Count; index++)
        {
            boundary += clip.FrameDurations[index];
            if (elapsed < boundary)
            {
                _frameIndex = index;
                return;
            }
        }

        _frameIndex = clip.FrameDurations.Count - 1;
    }

    private static double NormalizeWidth(double width) =>
        double.IsFinite(width) && width > 0 ? width : 0;

    private enum PendingAction
    {
        None,
        Walk,
    }
}
