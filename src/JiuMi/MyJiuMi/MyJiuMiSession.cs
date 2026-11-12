using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using JiuMi.Application.MyJiuMi;
using JiuMi.Application.Settings;
using JiuMi.Views;

namespace JiuMi.MyJiuMi;

/// <summary>
/// The single My JiuMi session shared by the page and the desktop window: the cat state machine, the
/// decoded frames and the 16 ms clock, plus the handoff between the two hosts.
/// </summary>
/// <remarks>
/// The page view model and <see cref="MyJiuMiDesktopWindow"/> are rendering and input adapters over
/// this object. The timer runs while the cat is active in either host; the desktop window is created
/// only for a handoff and is closed as soon as the cat comes home or the application exits.
/// </remarks>
public sealed class MyJiuMiSession : ObservableObject, IDisposable
{
    private static readonly Uri _assetRoot = new("avares://JiuMi/Assets/MyJiuMi/");

    private readonly ISettingsStore _settingsStore;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;
    private readonly MyJiuMiPresenceMachine _presence = new();
    private readonly MyJiuMiStateMachine _machine;
    private readonly Dictionary<MyJiuMiState, Bitmap?[]> _frames = [];
    private readonly HashSet<MyJiuMiState> _failedStates = [];

    private DispatcherTimer? _timer;
    private MainWindow? _shell;
    private IMyJiuMiHomeHost? _homeHost;
    private MyJiuMiDesktopWindow? _desktopWindow;
    private Bitmap? _frame;
    private bool _isFrameVisible;
    private bool _isHeartVisible;
    private bool _isZzzVisible;
    private bool _isFacingRight;
    private double _positionX = MyJiuMiPositionRules.Default;
    private double _homeScale = 1;
    private double _desktopOpacity = 1;
    private double _desktopSizeDip = MyJiuMiTransitionRules.DesktopSizeDip;
    private double _homeStageWidth;
    private DateTimeOffset? _heartRevealAt;
    private DateTimeOffset? _heartHideAt;
    private DateTimeOffset _leaveStartedAt;
    private DateTimeOffset _motionStartedAt;
    private DateTimeOffset? _jumpStartedAt;
    private MyJiuMiMotion _motion = MyJiuMiMotion.None;
    private MyJiuMiLeaveTrigger? _leaveTrigger;
    private MyJiuMiState? _renderedState;
    private int _renderedFrame = -1;
    private bool _renderedJumping;
    private DesktopEntranceRun? _desktopEntrance;
    private DesktopWalk? _desktopWalk;
    private TrayLeave? _trayLeave;
    private PendingHandoff? _pendingHandoff;
    private DispatcherTimer? _handoffTimer;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyJiuMiSession"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies and stores the normalized cat position.</param>
    /// <param name="timeProvider">Supplies the animation and overlay clock.</param>
    /// <param name="logger">Receives frame, handoff and screen warnings.</param>
    public MyJiuMiSession(
        ISettingsStore settingsStore,
        TimeProvider timeProvider,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _timeProvider = timeProvider;
        _logger = logger;
        _machine = new MyJiuMiStateMachine(timeProvider, Random.Shared);
    }

    /// <summary>
    /// Raised when the desktop cat asks for the My JiuMi page; the shell restores and navigates.
    /// </summary>
    public event EventHandler? OpenHomeRequested;

    /// <summary>
    /// Gets where the cat currently belongs.
    /// </summary>
    public MyJiuMiPresence Presence => _presence.Presence;

    /// <summary>
    /// Gets a value indicating whether the state machine is running.
    /// </summary>
    public bool IsMachineActive => _machine.IsActive;

    /// <summary>
    /// Gets a value indicating whether the cat is awake and may leave the page.
    /// </summary>
    public bool IsAwake => _machine.IsAwake;

    /// <summary>
    /// Gets the frame to draw, or <see langword="null"/> when the state's frames are unavailable.
    /// </summary>
    public Bitmap? Frame
    {
        get => _frame;
        private set => SetProperty(ref _frame, value);
    }

    /// <summary>
    /// Gets a value indicating whether the loaded frames have a frame for the current state.
    /// </summary>
    public bool IsFrameVisible
    {
        get => _isFrameVisible;
        private set => SetProperty(ref _isFrameVisible, value);
    }

    /// <summary>
    /// Gets a value indicating whether the heart overlay is showing.
    /// </summary>
    public bool IsHeartVisible
    {
        get => _isHeartVisible;
        private set => SetProperty(ref _isHeartVisible, value);
    }

    /// <summary>
    /// Gets a value indicating whether the <c>Zzz</c> overlay is showing.
    /// </summary>
    public bool IsZzzVisible
    {
        get => _isZzzVisible;
        private set => SetProperty(ref _isZzzVisible, value);
    }

    /// <summary>
    /// Gets a value indicating whether the walking frames should be mirrored to face right.
    /// </summary>
    public bool IsFacingRight
    {
        get => _isFacingRight;
        private set => SetProperty(ref _isFacingRight, value);
    }

    /// <summary>
    /// Gets the cat's horizontal position as a fraction of the home room width.
    /// </summary>
    public double PositionX
    {
        get => _positionX;
        private set => SetProperty(ref _positionX, value);
    }

    /// <summary>
    /// Gets how far the page cat has shrunk while walking out, used by the page host.
    /// </summary>
    public double HomeScale
    {
        get => _homeScale;
        private set => SetProperty(ref _homeScale, value);
    }

    /// <summary>
    /// Gets the procedural micro-motion of the page cat; the desktop host ignores it.
    /// </summary>
    public MyJiuMiMotion Motion
    {
        get => _motion;
        private set => SetProperty(ref _motion, value);
    }

    /// <summary>
    /// Gets the desktop window opacity used by the minimized entrance.
    /// </summary>
    public double DesktopOpacity
    {
        get => _desktopOpacity;
        private set => SetProperty(ref _desktopOpacity, value);
    }

    /// <summary>
    /// Attaches the shell window so the session can read the screens and clamp the desktop window.
    /// </summary>
    /// <param name="shell">The main window.</param>
    public void AttachShell(MainWindow shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        if (_shell is not null)
        {
            return;
        }

        _shell = shell;
        shell.Screens.Changed += OnScreensChanged;
    }

    /// <summary>
    /// Registers the page host that supplies the cat's screen anchor.
    /// </summary>
    /// <param name="host">The attached My JiuMi page.</param>
    public void AttachHomeHost(IMyJiuMiHomeHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _homeHost = host;
    }

    /// <summary>
    /// Forgets a page host that left the visual tree.
    /// </summary>
    /// <param name="host">The detached My JiuMi page.</param>
    public void DetachHomeHost(IMyJiuMiHomeHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (ReferenceEquals(_homeHost, host))
        {
            _homeHost = null;
        }
    }

    /// <summary>
    /// Starts a fresh home visit from <see cref="MyJiuMiState.LyingIdle"/> at the saved position, or
    /// updates the room width while the cat stays at home.
    /// </summary>
    /// <param name="stageWidth">The room width in DIP.</param>
    public void ActivateHome(double stageWidth)
    {
        if (_isDisposed || !(stageWidth > 0))
        {
            return;
        }

        SetHomeStageWidth(stageWidth);
        if (Presence != MyJiuMiPresence.Home)
        {
            return;
        }

        _machine.SetWanderingEnabled(true);
        HomeScale = 1;
        if (!_machine.IsActive)
        {
            ClearHeart();
            double position = MyJiuMiPositionRules.Clamp(_settingsStore.Current.MyJiuMi.X);
            _machine.Activate(stageWidth, position);
            StartMotion();
            StartTimer();
        }

        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Pauses the cat when the home page is left or hidden; the desktop presence is untouched.
    /// </summary>
    public void DeactivateHome()
    {
        if (Presence != MyJiuMiPresence.Home)
        {
            return;
        }

        StopMachine();
        HomeScale = 1;
        OnPropertyChanged(nameof(IsMachineActive));
    }

    /// <summary>
    /// Updates the home room width after a resize. The stored normalized position is unchanged.
    /// </summary>
    /// <param name="stageWidth">The new room width in DIP.</param>
    public void SetHomeStageWidth(double stageWidth)
    {
        if (!(stageWidth > 0))
        {
            return;
        }

        _homeStageWidth = stageWidth;
        _machine.SetStageWidth(stageWidth);
    }

    /// <summary>
    /// Applies a click on the cat and schedules the heart when the cat reacted.
    /// </summary>
    public void Click()
    {
        if (Presence is MyJiuMiPresence.Leaving or MyJiuMiPresence.Returning)
        {
            return;
        }

        MyJiuMiClickEffect effect = _machine.Click();
        if (effect == MyJiuMiClickEffect.React)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            _heartRevealAt = now + MyJiuMiInteractionRules.HeartDelay;
            _heartHideAt = _heartRevealAt + MyJiuMiInteractionRules.HeartDuration;
            if (Presence == MyJiuMiPresence.Home)
            {
                StartJump(now);
            }
        }
        else
        {
            ClearHeart();
        }

        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Starts dragging the page cat; a sleeping cat is picked up directly.
    /// </summary>
    public void BeginHomeDrag()
    {
        if (Presence != MyJiuMiPresence.Home)
        {
            return;
        }

        ClearHeart();
        IsHeartVisible = false;
        EndJump();
        _machine.BeginDrag();
        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Moves the page cat to the pointer's normalized horizontal position.
    /// </summary>
    /// <param name="positionX">The normalized pointer position.</param>
    public void DragTo(double positionX)
    {
        if (Presence != MyJiuMiPresence.Home)
        {
            return;
        }

        _machine.SetPosition(positionX);
        UpdateVisuals(forceFrame: false);
    }

    /// <summary>
    /// Ends the page drag, saves the normalized position and settles back on the floor.
    /// </summary>
    public void EndHomeDrag()
    {
        if (Presence != MyJiuMiPresence.Home)
        {
            return;
        }

        _machine.EndDrag();
        SavePosition();
        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Summons the home cat to the double-clicked floor position.
    /// </summary>
    /// <param name="positionX">The normalized click position.</param>
    public void Summon(double positionX)
    {
        if (Presence != MyJiuMiPresence.Home)
        {
            return;
        }

        EndJump();
        _machine.Summon(positionX);
        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Starts dragging the desktop cat; the window, not the normalized home position, is moved.
    /// </summary>
    public void BeginDesktopDrag()
    {
        FinishDesktopEntrance();
        _desktopWalk = null;
        ClearHeart();
        IsHeartVisible = false;
        EndJump();
        _machine.BeginDrag();
        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Ends the desktop drag without writing the home position.
    /// </summary>
    public void EndDesktopDrag()
    {
        _machine.EndDrag();
        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Clamps the desktop window into the nearest screen work area after a drag, screen change or DPI change.
    /// </summary>
    public void ClampDesktopWindow()
    {
        if (_desktopWindow is not { } window)
        {
            return;
        }

        FinishDesktopEntrance();
        IReadOnlyList<MyJiuMiScreenArea> workAreas = ReadWorkAreas();
        if (workAreas.Count == 0)
        {
            return;
        }

        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.ResolveOrigin(
            new MyJiuMiScreenPoint(window.Position.X, window.Position.Y),
            workAreas);
        window.Position = new PixelPoint(placement.Origin.X, placement.Origin.Y);

        if (_desktopWalk is not null)
        {
            int pixelSize = MyJiuMiTransitionRules.PixelSize(placement.Area.Scaling);
            _desktopWalk = new DesktopWalk(
                new MyJiuMiScreenPoint(
                    placement.Origin.X + (pixelSize / 2),
                    placement.Origin.Y + pixelSize),
                _machine.PositionX,
                placement.Area);
        }
    }

    /// <summary>
    /// Asks the shell to restore the main window on the My JiuMi page without recalling the cat.
    /// </summary>
    public void RequestOpenHome() => OpenHomeRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Walks the cat out after the main window was minimized: the desktop window fades in at the last
    /// known position and the cat finishes a short walk.
    /// </summary>
    /// <param name="pageCurrent">Whether the My JiuMi page is the page currently shown.</param>
    /// <returns><see langword="true"/> when the desktop host took over.</returns>
    public bool TryLeaveOnMinimize(bool pageCurrent)
    {
        if (!TryBeginLeave(MyJiuMiLeaveTrigger.Minimized, pageCurrent))
        {
            return false;
        }

        if (!EnsureDesktopFrames())
        {
            CancelLeave();
            return false;
        }

        PixelPoint? anchor = _homeHost?.GetCatScreenAnchor();
        if (anchor is null)
        {
            CancelLeave();
            return false;
        }

        return OpenDesktop(anchor.Value, DesktopEntrance.Minimized);
    }

    /// <summary>
    /// Walks the cat to the nearest room edge before hiding to the tray and invokes
    /// <paramref name="ready"/> once the handoff is done or the walk timed out. The callback runs on
    /// the interface thread and must hide the shell; it is always invoked exactly once.
    /// </summary>
    /// <param name="pageCurrent">Whether the My JiuMi page is the page currently shown.</param>
    /// <param name="ready">Hides the shell after the handoff.</param>
    public void BeginTrayLeave(bool pageCurrent, Action ready)
    {
        ArgumentNullException.ThrowIfNull(ready);
        if (_trayLeave is not null)
        {
            return;
        }

        if (!TryBeginLeave(MyJiuMiLeaveTrigger.HiddenToTray, pageCurrent) ||
            !EnsureDesktopFrames() ||
            _homeHost?.GetCatScreenAnchor() is null)
        {
            CancelLeave();
            ready();
            return;
        }

        double edge = MyJiuMiTransitionRules.NearestEdge(_machine.PositionX);
        _machine.WalkTo(edge);
        _trayLeave = new TrayLeave(
            _timeProvider.GetUtcNow() +
                MyJiuMiTransitionRules.TrayHideTimeout -
                MyJiuMiTransitionRules.TrayHandoffReserve,
            ready);
    }

    /// <summary>
    /// Closes the desktop window, stops the clock and returns the presence to home as the application exits.
    /// </summary>
    public void Shutdown()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _trayLeave = null;
        StopHandoffTimer();
        _pendingHandoff = null;
        if (_shell is { } shell)
        {
            shell.Screens.Changed -= OnScreensChanged;
            _shell = null;
        }

        CloseDesktopWindow();
        StopMachine();
        HomeScale = 1;
        _presence.Reset();
        NotifyPresence();

        foreach (Bitmap?[] frames in _frames.Values)
        {
            foreach (Bitmap? frame in frames)
            {
                frame?.Dispose();
            }
        }

        _frames.Clear();
        _failedStates.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Shutdown();
        GC.SuppressFinalize(this);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _machine.Update();
        UpdateMotion();
        UpdateVisuals(forceFrame: false);
        UpdateHomeScale();
        UpdateTrayLeave();
        UpdateDesktopEntrance();
        UpdateDesktopWalk();
    }

    private void StartMotion()
    {
        _motionStartedAt = _timeProvider.GetUtcNow();
        _jumpStartedAt = null;
        _renderedJumping = false;
        ResetMotion();
    }

    private void UpdateMotion()
    {
        if (_isDisposed || !_machine.IsActive || Presence == MyJiuMiPresence.Desktop)
        {
            ResetMotion();
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_jumpStartedAt is { } jumpStartedAt)
        {
            if (now - jumpStartedAt >= MyJiuMiMotionRules.JumpDuration)
            {
                _jumpStartedAt = null;
            }
            else
            {
                Motion = MyJiuMiMotionRules.SampleJump(now - jumpStartedAt);
                return;
            }
        }

        Motion = MyJiuMiMotionRules.Sample(now - _motionStartedAt, _machine.State);
    }

    private void StartJump(DateTimeOffset now)
    {
        if (CanJump())
        {
            _jumpStartedAt = now;
        }
    }

    private void EndJump() => _jumpStartedAt = null;

    private bool IsJumpActive(DateTimeOffset now) =>
        Presence == MyJiuMiPresence.Home &&
        _jumpStartedAt is { } startedAt &&
        now - startedAt < MyJiuMiMotionRules.JumpDuration;

    private bool CanJump()
    {
        Bitmap?[]? frames = GetFrames(MyJiuMiState.Stretch);
        return frames is not null &&
            frames.Length > MyJiuMiMotionRules.JumpFrameIndex &&
            frames[MyJiuMiMotionRules.JumpFrameIndex] is not null;
    }

    private void ResetMotion() => Motion = MyJiuMiMotion.None;

    private void UpdateHomeScale()
    {
        if (Presence != MyJiuMiPresence.Leaving ||
            _leaveTrigger != MyJiuMiLeaveTrigger.HiddenToTray)
        {
            return;
        }

        double walkMilliseconds =
            (MyJiuMiTransitionRules.TrayHideTimeout - MyJiuMiTransitionRules.TrayHandoffReserve)
            .TotalMilliseconds;
        double elapsed = (_timeProvider.GetUtcNow() - _leaveStartedAt).TotalMilliseconds;
        double progress = Math.Clamp(elapsed / walkMilliseconds, 0, 1);
        HomeScale = 1 - (0.2 * progress);
    }

    private void UpdateTrayLeave()
    {
        if (_trayLeave is not { } leave)
        {
            return;
        }

        if (_isDisposed)
        {
            _trayLeave = null;
            return;
        }

        if (_machine.State == MyJiuMiState.Walk &&
            _timeProvider.GetUtcNow() < leave.Deadline)
        {
            return;
        }

        _trayLeave = null;
        PixelPoint? anchor = _homeHost?.GetCatScreenAnchor();
        MyJiuMiDesktopWindow? prepared = null;
        MyJiuMiScreenArea area = default;
        if (anchor is { } value)
        {
            prepared = PrepareDesktop(value, DesktopEntrance.Hidden, out area);
        }

        if (prepared is null || anchor is null)
        {
            leave.Ready();
            CancelLeave();
            if (_homeHost is not { IsHomeVisible: true })
            {
                StopMachine();
                OnPropertyChanged(nameof(IsMachineActive));
            }

            return;
        }

        // Empty the page first and give the compositor a couple of frames, then hide the shell and
        // only afterwards show the desktop cat. Otherwise the old window's last frame can linger
        // over the new host for a frame and read as a second, differently sized cat.
        _presence.TryCompleteLeave();
        NotifyPresence();
        _pendingHandoff = new PendingHandoff(prepared, anchor.Value, area, leave.Ready);
        _handoffTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(32),
            DispatcherPriority.Background,
            OnHandoffTick);
        _handoffTimer.Start();
    }

    private void OnHandoffTick(object? sender, EventArgs e)
    {
        StopHandoffTimer();
        PendingHandoff? pending = _pendingHandoff;
        _pendingHandoff = null;
        if (_isDisposed || pending is not { } handoff)
        {
            return;
        }

        handoff.HideShell();
        if (!ReferenceEquals(_desktopWindow, handoff.Window))
        {
            return;
        }

        BeginDesktopEntrance(DesktopEntrance.Hidden, handoff.Anchor, handoff.Area);
        StartTimer();
        UpdateVisuals(forceFrame: true);
    }

    private void StopHandoffTimer()
    {
        if (_handoffTimer is not { } timer)
        {
            return;
        }

        timer.Stop();
        _handoffTimer = null;
    }

    private void UpdateVisuals(bool forceFrame)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!_machine.IsActive)
        {
            IsFrameVisible = false;
            Frame = null;
            IsFacingRight = false;
            IsZzzVisible = false;
            return;
        }

        MyJiuMiState state = _machine.State;
        int frameIndex = _machine.FrameIndex;
        DateTimeOffset now = _timeProvider.GetUtcNow();
        bool jumping = IsJumpActive(now);
        if (forceFrame ||
            _renderedState != state ||
            _renderedFrame != frameIndex ||
            _renderedJumping != jumping)
        {
            MyJiuMiState renderState = jumping ? MyJiuMiState.Stretch : state;
            int renderFrame = jumping ? MyJiuMiMotionRules.JumpFrameIndex : frameIndex;
            Bitmap?[]? frames = GetFrames(renderState);
            if (frames is null || renderFrame >= frames.Length)
            {
                IsFrameVisible = false;
                Frame = null;
                if (Presence == MyJiuMiPresence.Desktop)
                {
                    HandleDesktopFrameLoss();
                    return;
                }
            }
            else
            {
                IsFrameVisible = true;
                Frame = frames[renderFrame];
            }

            _renderedState = state;
            _renderedFrame = frameIndex;
            _renderedJumping = jumping;
        }

        IsFacingRight = _machine.IsFacingRight;
        PositionX = _machine.PositionX;

        if (_heartHideAt is { } hideAt && now >= hideAt)
        {
            ClearHeart();
        }

        IsHeartVisible =
            _heartRevealAt is { } revealAt &&
            _heartHideAt is not null &&
            now >= revealAt;
        IsZzzVisible = state == MyJiuMiState.Sleep;
    }

    private bool TryBeginLeave(MyJiuMiLeaveTrigger trigger, bool pageCurrent)
    {
        if (_isDisposed ||
            !_machine.IsActive ||
            _machine.State == MyJiuMiState.Drag ||
            !_presence.TryBeginLeave(trigger, pageCurrent, _machine.IsAwake, desktopAvailable: true))
        {
            return false;
        }

        _machine.SetWanderingEnabled(false);
        EndJump();
        _leaveStartedAt = _timeProvider.GetUtcNow();
        _leaveTrigger = trigger;
        HomeScale = 1;
        ClearHeart();
        IsHeartVisible = false;
        NotifyPresence();
        return true;
    }

    private bool EnsureDesktopFrames()
    {
        if (GetFrames(MyJiuMiState.LyingIdle) is not null && GetFrames(MyJiuMiState.Walk) is not null)
        {
            return true;
        }

        _logger.Warning("The desktop host was cancelled because the My JiuMi frames are unavailable");
        return false;
    }

    private void CancelLeave()
    {
        if (_presence.TryCancelLeave())
        {
            _machine.SetWanderingEnabled(true);
            _leaveTrigger = null;
            HomeScale = 1;
            NotifyPresence();
        }
    }

    private bool OpenDesktop(PixelPoint anchor, DesktopEntrance entrance)
    {
        if (_isDisposed)
        {
            CancelLeave();
            return false;
        }

        if (_desktopWindow is not null)
        {
            if (_presence.TryCompleteLeave())
            {
                NotifyPresence();
            }

            return true;
        }

        MyJiuMiDesktopWindow? window = PrepareDesktop(anchor, entrance, out MyJiuMiScreenArea area);
        if (window is null)
        {
            CancelLeave();
            return false;
        }

        return CompleteDesktop(window, anchor, area, entrance);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Window creation is a UI boundary; a missing host must keep the cat at home.")]
    private MyJiuMiDesktopWindow? PrepareDesktop(
        PixelPoint anchor,
        DesktopEntrance entrance,
        out MyJiuMiScreenArea area)
    {
        area = default;
        if (!EnsureDesktopFrames())
        {
            return null;
        }

        IReadOnlyList<MyJiuMiScreenArea> workAreas = ReadWorkAreas();
        if (workAreas.Count == 0)
        {
            _logger.Warning("The desktop host was cancelled because no screen work area is available");
            return null;
        }

        double startSizeDip = entrance == DesktopEntrance.Minimized
            ? MyJiuMiTransitionRules.HomeSizeDip
            : MyJiuMiTransitionRules.DesktopSizeDip;
        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.PlaceFromAnchor(
            new MyJiuMiScreenPoint(anchor.X, anchor.Y),
            startSizeDip,
            workAreas);
        area = placement.Area;

        MyJiuMiDesktopWindow window;
        try
        {
            window = new MyJiuMiDesktopWindow(this);
        }
        catch (Exception exception)
        {
            _logger.Warning(
                exception,
                "The My JiuMi desktop window could not be created; the cat stays home");
            return null;
        }

        _desktopWindow = window;
        window.Closed += OnDesktopWindowClosed;
        _desktopSizeDip = startSizeDip;
        window.Width = startSizeDip;
        window.Height = startSizeDip;
        window.Position = new PixelPoint(placement.Origin.X, placement.Origin.Y);

        // The host stays invisible until transparency is confirmed, so an unsupported compositor
        // never flashes an opaque box and the caller may hide the old host first.
        DesktopOpacity = 0;

        try
        {
            window.Show();
        }
        catch (Exception exception)
        {
            _logger.Warning(
                exception,
                "The My JiuMi desktop window could not be shown; the cat stays home");
            CloseDesktopWindow();
            return null;
        }

        if (window.ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
        {
            _logger.Warning("The My JiuMi desktop window requires transparency; the cat stays home");
            CloseDesktopWindow();
            return null;
        }

        return window;
    }

    private bool CompleteDesktop(
        MyJiuMiDesktopWindow window,
        PixelPoint anchor,
        MyJiuMiScreenArea area,
        DesktopEntrance entrance)
    {
        if (!ReferenceEquals(_desktopWindow, window))
        {
            return false;
        }

        _presence.TryCompleteLeave();
        NotifyPresence();
        BeginDesktopEntrance(entrance, anchor, area);
        StartTimer();
        UpdateVisuals(forceFrame: true);
        return true;
    }

    private void BeginDesktopEntrance(
        DesktopEntrance entrance,
        PixelPoint anchor,
        MyJiuMiScreenArea area)
    {
        // The cat keeps walking a short way while the window glides with it; the window, not the
        // normalized home position, carries the cat across the desktop.
        double direction = _machine.State == MyJiuMiState.Walk
            ? (_machine.IsFacingRight ? 1 : -1)
            : (_machine.PositionX < 0.5 ? 1 : -1);
        double distance = _homeStageWidth > 0
            ? MyJiuMiTransitionRules.DesktopWalkDistanceDip / _homeStageWidth
            : 0.05;
        double startX = _machine.PositionX;
        _machine.WalkTo(startX + (direction * distance));
        _desktopWalk = new DesktopWalk(
            new MyJiuMiScreenPoint(anchor.X, anchor.Y),
            startX,
            area);

        if (entrance == DesktopEntrance.Minimized)
        {
            _desktopEntrance = new DesktopEntranceRun(
                new MyJiuMiScreenPoint(anchor.X, anchor.Y),
                area,
                _timeProvider.GetUtcNow(),
                MyJiuMiTransitionRules.MinimizeEntrance);
            DesktopOpacity = 0;
            return;
        }

        // The page cat already shrank to 160 DIP during the walk; the desktop host starts at the
        // same size, so the handoff does not jump.
        _desktopEntrance = null;
        _desktopSizeDip = MyJiuMiTransitionRules.DesktopSizeDip;
        HomeScale = MyJiuMiTransitionRules.DesktopSizeDip / MyJiuMiTransitionRules.HomeSizeDip;
        DesktopOpacity = 1;
    }

    private void UpdateDesktopEntrance()
    {
        if (_desktopEntrance is not { } run || _desktopWindow is not { } window)
        {
            return;
        }

        double progress = run.GetProgress(_timeProvider.GetUtcNow());
        _desktopSizeDip = MyJiuMiTransitionRules.HomeSizeDip +
            ((MyJiuMiTransitionRules.DesktopSizeDip - MyJiuMiTransitionRules.HomeSizeDip) * progress);
        window.Width = _desktopSizeDip;
        window.Height = _desktopSizeDip;
        DesktopOpacity = progress;

        if (progress >= 1)
        {
            _desktopSizeDip = MyJiuMiTransitionRules.DesktopSizeDip;
            window.Width = _desktopSizeDip;
            window.Height = _desktopSizeDip;
            DesktopOpacity = 1;
            _desktopEntrance = null;
        }
    }

    private void FinishDesktopEntrance()
    {
        if (_desktopEntrance is not { } run)
        {
            return;
        }

        _desktopEntrance = null;
        _desktopSizeDip = MyJiuMiTransitionRules.DesktopSizeDip;
        DesktopOpacity = 1;

        if (_desktopWindow is { } window)
        {
            window.Width = _desktopSizeDip;
            window.Height = _desktopSizeDip;
            IReadOnlyList<MyJiuMiScreenArea> workAreas = ReadWorkAreas();
            if (workAreas.Count > 0)
            {
                MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.PlaceFromAnchor(
                    new MyJiuMiScreenPoint(run.Anchor.X, run.Anchor.Y),
                    workAreas);
                window.Position = new PixelPoint(placement.Origin.X, placement.Origin.Y);
            }
        }
    }

    private void UpdateDesktopWalk()
    {
        if (_desktopWindow is not { } window ||
            (_desktopWalk is null && _desktopEntrance is null))
        {
            return;
        }

        MyJiuMiScreenPoint anchor;
        MyJiuMiScreenArea area;
        double offsetDip = 0;
        if (_desktopWalk is { } walk)
        {
            anchor = walk.Anchor;
            area = walk.Area;
            offsetDip = (_machine.PositionX - walk.StartPositionX) * _homeStageWidth;
            if (_machine.State != MyJiuMiState.Walk)
            {
                _desktopWalk = null;
            }
        }
        else if (_desktopEntrance is { } run)
        {
            anchor = run.Anchor;
            area = run.Area;
        }
        else
        {
            return;
        }

        IReadOnlyList<MyJiuMiScreenArea> workAreas = ReadWorkAreas();
        if (workAreas.Count == 0)
        {
            return;
        }

        int pixelSize = MyJiuMiTransitionRules.PixelSize(_desktopSizeDip, area.Scaling);
        var desired = new MyJiuMiScreenPoint(
            anchor.X +
                (int)Math.Round(offsetDip * area.Scaling, MidpointRounding.AwayFromZero) -
                (pixelSize / 2),
            anchor.Y - pixelSize);
        MyJiuMiDesktopPlacement placement = MyJiuMiTransitionRules.ResolveOrigin(desired, workAreas);
        window.Position = new PixelPoint(placement.Origin.X, placement.Origin.Y);
    }

    private void CloseDesktopWindow()
    {
        StopHandoffTimer();
        _pendingHandoff = null;
        if (_desktopWindow is not { } window)
        {
            return;
        }

        _desktopWindow = null;
        _desktopWalk = null;
        _desktopEntrance = null;
        try
        {
            window.Close();
        }
        catch (InvalidOperationException exception)
        {
            _logger.Debug(exception, "The My JiuMi desktop window was already gone");
        }
    }

    private void HandleDesktopFrameLoss()
    {
        _logger.Warning("A desktop My JiuMi frame is unavailable; the cat returns home");
        ReturnHomeFromDesktop();
    }

    private void OnDesktopWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not MyJiuMiDesktopWindow window)
        {
            return;
        }

        window.Closed -= OnDesktopWindowClosed;
        if (!ReferenceEquals(_desktopWindow, window) || _isDisposed)
        {
            return;
        }

        _desktopWindow = null;
        _desktopWalk = null;
        _desktopEntrance = null;
        _logger.Warning("The My JiuMi desktop window closed unexpectedly; the cat returns home");
        ReturnHomeFromDesktop();
    }

    private IReadOnlyList<MyJiuMiScreenArea> ReadWorkAreas()
    {
        Screens? screens = _shell?.Screens;
        if (screens is null)
        {
            return [];
        }

        return
        [
            .. screens.All.Select(screen => new MyJiuMiScreenArea(
                screen.WorkingArea.X,
                screen.WorkingArea.Y,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height,
                screen.Scaling,
                screen.IsPrimary)),
        ];
    }

    private void OnScreensChanged(object? sender, EventArgs e) => ClampDesktopWindow();

    private void StartTimer()
    {
        _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void StopTimer() => _timer?.Stop();

    private void StopMachine()
    {
        StopTimer();
        _machine.Deactivate();
        ClearHeart();
        IsHeartVisible = false;
        IsZzzVisible = false;
        IsFrameVisible = false;
        Frame = null;
        _renderedState = null;
        _renderedFrame = -1;
        _renderedJumping = false;
        EndJump();
        ResetMotion();
    }

    private void ReturnHomeFromDesktop()
    {
        CloseDesktopWindow();
        _presence.Reset();
        StopMachine();
        HomeScale = 1;
        _leaveTrigger = null;

        // When the page is already on screen the cat must reappear at home now; otherwise the next
        // activation does it from the saved position.
        if (_homeHost is { IsHomeVisible: true } && _homeStageWidth > 0)
        {
            _machine.SetWanderingEnabled(true);
            _machine.Activate(
                _homeStageWidth,
                MyJiuMiPositionRules.Clamp(_settingsStore.Current.MyJiuMi.X));
            StartMotion();
            StartTimer();
            UpdateVisuals(forceFrame: true);
        }

        NotifyPresence();
    }

    private void NotifyPresence()
    {
        OnPropertyChanged(nameof(Presence));
        OnPropertyChanged(nameof(IsMachineActive));
        OnPropertyChanged(nameof(IsAwake));
    }

    private void ClearHeart()
    {
        _heartRevealAt = null;
        _heartHideAt = null;
    }

    private void SavePosition()
    {
        AppSettings settings = _settingsStore.Current with
        {
            MyJiuMi = new MyJiuMiSettings
            {
                X = MyJiuMiPositionRules.Clamp(_machine.PositionX),
            },
        };

        SettingsSaveResult result = _settingsStore.Save(settings);
        if (result.Error is { } error)
        {
            _logger
                .ForContext("operation", "MyJiuMi.SavePosition")
                .Warning(error, "My JiuMi position save failed");
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Asset loading is a UI boundary; a missing frame must degrade to a hidden cat.")]
    private Bitmap?[]? GetFrames(MyJiuMiState state)
    {
        if (_isDisposed)
        {
            return null;
        }

        if (_frames.TryGetValue(state, out Bitmap?[]? frames))
        {
            return frames;
        }

        if (_failedStates.Contains(state))
        {
            return null;
        }

        MyJiuMiAnimationClip clip = MyJiuMiAnimationCatalog.GetClip(state);
        var loaded = new Bitmap?[clip.FrameDurations.Count];
        try
        {
            for (int index = 0; index < loaded.Length; index++)
            {
                Uri uri = new(_assetRoot, $"{clip.AssetFolder}/{index:D2}.png");
                using Stream stream = AssetLoader.Open(uri);
                loaded[index] = new Bitmap(stream);
            }

            _frames[state] = loaded;
            return loaded;
        }
        catch (Exception exception)
        {
            foreach (Bitmap? frame in loaded)
            {
                frame?.Dispose();
            }

            _failedStates.Add(state);
            _logger.Warning(
                exception,
                "The My JiuMi frames for {State} could not be loaded; the cat stays hidden",
                state);
            return null;
        }
    }

    private enum DesktopEntrance
    {
        Hidden,
        Minimized,
    }

    private readonly record struct DesktopEntranceRun(
        MyJiuMiScreenPoint Anchor,
        MyJiuMiScreenArea Area,
        DateTimeOffset StartedAt,
        TimeSpan Duration)
    {
        public double GetProgress(DateTimeOffset now)
        {
            if (Duration <= TimeSpan.Zero)
            {
                return 1;
            }

            double progress = (now - StartedAt).TotalSeconds / Duration.TotalSeconds;
            return Math.Clamp(progress, 0, 1);
        }
    }

    private readonly record struct DesktopWalk(
        MyJiuMiScreenPoint Anchor,
        double StartPositionX,
        MyJiuMiScreenArea Area);

    private readonly record struct TrayLeave(DateTimeOffset Deadline, Action Ready);

    private readonly record struct PendingHandoff(
        MyJiuMiDesktopWindow Window,
        PixelPoint Anchor,
        MyJiuMiScreenArea Area,
        Action HideShell);
}
