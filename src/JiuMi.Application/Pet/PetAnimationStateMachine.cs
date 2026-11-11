using System.Diagnostics.CodeAnalysis;

namespace JiuMi.Application.Pet;

/// <summary>
/// Decides which desktop pet animation plays and when the pet blinks, sleeps or wakes.
/// </summary>
/// <remarks>
/// The machine owns no timers and draws nothing: the window calls <see cref="Update"/> about ten times
/// a second and renders <see cref="State"/> and <see cref="FrameIndex"/>. Time and randomness are
/// injected so the transitions are unit-testable without a real clock.
/// </remarks>
public sealed class PetAnimationStateMachine
{
    private const int MaxTransitionsPerUpdate = 16;

    private readonly TimeProvider _timeProvider;
    private readonly Random _random;

    private PetState _state;
    private DateTimeOffset _stateStartedAt;
    private DateTimeOffset _sleepDeadline;
    private DateTimeOffset? _nextBlinkAt;
    private int _frameIndex;
    private bool _isSuspended;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetAnimationStateMachine"/> class in
    /// <see cref="PetState.Idle"/>.
    /// </summary>
    /// <param name="timeProvider">Supplies the current time.</param>
    /// <param name="random">Picks the delay between blinks.</param>
    public PetAnimationStateMachine(TimeProvider timeProvider, Random random)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(random);

        _timeProvider = timeProvider;
        _random = random;

        DateTimeOffset now = timeProvider.GetUtcNow();
        EnterState(PetState.Idle, now);
        ResetSleep(now);
    }

    /// <summary>
    /// Gets the animation currently playing.
    /// </summary>
    public PetState State => _state;

    /// <summary>
    /// Gets the zero-based frame of <see cref="State"/> to draw.
    /// </summary>
    public int FrameIndex => _frameIndex;

    /// <summary>
    /// Advances the animation to the current time: changes frames, blinks and falls asleep when due.
    /// </summary>
    public void Update()
    {
        if (_isSuspended)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        for (int transitions = 0; transitions < MaxTransitionsPerUpdate; transitions++)
        {
            if ((_state is PetState.Idle or PetState.Blink) && now >= _sleepDeadline)
            {
                EnterState(PetState.SleepIn, now);
                continue;
            }

            if (_state == PetState.Idle && _nextBlinkAt is { } blinkAt && now >= blinkAt)
            {
                EnterState(PetState.Blink, now);
                continue;
            }

            PetAnimationClip clip = PetAnimationCatalog.For(_state);
            TimeSpan elapsed = now - _stateStartedAt;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            if (clip.Loops || elapsed < clip.Duration)
            {
                _frameIndex = FrameAt(clip, elapsed);
                return;
            }

            DateTimeOffset completedAt = _stateStartedAt + clip.Duration;
            switch (_state)
            {
                case PetState.Blink:
                case PetState.React:
                case PetState.Wake:
                    EnterState(PetState.Idle, completedAt);
                    break;
                case PetState.SleepIn:
                    EnterState(PetState.Sleep, completedAt);
                    break;
                default:
                    return;
            }
        }
    }

    /// <summary>
    /// Starts from the shell: enter the desktop with the walking loop until
    /// <see cref="CompleteEntrance"/> is called.
    /// </summary>
    public void BeginEntrance()
    {
        if (_isSuspended)
        {
            return;
        }

        EnterState(PetState.WalkOut, _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Ends the entrance at the landing position and starts the sleep countdown.
    /// </summary>
    public void CompleteEntrance()
    {
        if (_isSuspended || _state != PetState.WalkOut)
        {
            return;
        }

        LeaveEntrance();
    }

    /// <summary>
    /// Stops the walking loop because an interaction took over; the click or drag decides the next state.
    /// </summary>
    public void InterruptEntrance()
    {
        if (_isSuspended || _state != PetState.WalkOut)
        {
            return;
        }

        LeaveEntrance();
    }

    /// <summary>
    /// Applies a single click after the double-click window has passed.
    /// </summary>
    /// <returns>Whether the pet reacted with a heart or started to wake up.</returns>
    public PetClickEffect Click()
    {
        if (_isSuspended || _state == PetState.Drag)
        {
            return PetClickEffect.None;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ResetSleep(now);
        if (_state is PetState.Sleep or PetState.SleepIn)
        {
            EnterState(PetState.Wake, now);
            return PetClickEffect.Wake;
        }

        EnterState(PetState.React, now);
        return PetClickEffect.React;
    }

    /// <summary>
    /// Applies a double click: wake a sleeping pet, and stop walking for a pet that is still entering.
    /// </summary>
    public void DoubleClick()
    {
        if (_isSuspended || _state == PetState.Drag)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ResetSleep(now);
        if (_state is PetState.Sleep or PetState.SleepIn or PetState.Wake)
        {
            EnterState(PetState.Wake, now);
        }
        else if (_state == PetState.WalkOut)
        {
            EnterState(PetState.Idle, now);
        }
    }

    /// <summary>
    /// Applies a right click before its menu opens: wake a sleeping pet, stop walking, and reset the
    /// sleep countdown without starting a reaction.
    /// </summary>
    public void RightClick()
    {
        if (_isSuspended || _state == PetState.Drag)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ResetSleep(now);
        if (_state is PetState.Sleep or PetState.SleepIn)
        {
            EnterState(PetState.Wake, now);
            return;
        }

        if (_state == PetState.WalkOut)
        {
            EnterState(PetState.Idle, now);
        }
    }

    /// <summary>
    /// Enters the dangling drag loop; a sleeping pet is picked up directly.
    /// </summary>
    public void BeginDrag()
    {
        if (_isSuspended)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        ResetSleep(now);
        EnterState(PetState.Drag, now);
    }

    /// <summary>
    /// Leaves the drag loop for <see cref="PetState.Idle"/> and restarts the sleep countdown.
    /// </summary>
    public void EndDrag()
    {
        if (_isSuspended || _state != PetState.Drag)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        EnterState(PetState.Idle, now);
        ResetSleep(now);
    }

    /// <summary>
    /// Shows the pet again: back to <see cref="PetState.Idle"/> with fresh timers.
    /// </summary>
    public void Show()
    {
        _isSuspended = false;
        DateTimeOffset now = _timeProvider.GetUtcNow();
        EnterState(PetState.Idle, now);
        ResetSleep(now);
    }

    /// <summary>
    /// Stops the clock while the pet is hidden; <see cref="Show"/> resumes at <see cref="PetState.Idle"/>.
    /// </summary>
    public void Hide() => _isSuspended = true;

    private void EnterState(PetState state, DateTimeOffset at)
    {
        _state = state;
        _stateStartedAt = at;
        _frameIndex = 0;
        _nextBlinkAt = state == PetState.Idle ? at + NextBlinkDelay() : null;
    }

    private void LeaveEntrance()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        EnterState(PetState.Idle, now);
        ResetSleep(now);
    }

    private void ResetSleep(DateTimeOffset now) => _sleepDeadline = now + PetInteractionRules.SleepDelay;

    [SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "The blink delay is cosmetic timing and has no security requirement.")]
    private TimeSpan NextBlinkDelay()
    {
        double fraction = _random.NextDouble();
        return PetInteractionRules.BlinkDelayMin +
            ((PetInteractionRules.BlinkDelayMax - PetInteractionRules.BlinkDelayMin) * fraction);
    }

    private static int FrameAt(PetAnimationClip clip, TimeSpan elapsed)
    {
        TimeSpan offset = elapsed;
        if (clip.Loops && clip.Duration > TimeSpan.Zero)
        {
            offset = TimeSpan.FromTicks(elapsed.Ticks % clip.Duration.Ticks);
        }

        TimeSpan boundary = TimeSpan.Zero;
        for (int index = 0; index < clip.FrameDurations.Count; index++)
        {
            boundary += clip.FrameDurations[index];
            if (offset < boundary)
            {
                return index;
            }
        }

        return clip.FrameDurations.Count - 1;
    }
}
