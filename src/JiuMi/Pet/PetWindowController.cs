using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using JiuMi.Application;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Pet;
using JiuMi.Application.Settings;
using JiuMi.Views;

namespace JiuMi.Pet;

/// <summary>
/// Owns the desktop pet window: lazy creation, entrance movement, animation frames, interactions,
/// visibility and the saved enabled/position state.
/// </summary>
/// <remarks>
/// The controller reads and writes the settings store and turns screen, shell and pointer state into a
/// window position and an animation frame. The window itself only renders and forwards pointer input.
/// </remarks>
public sealed class PetWindowController : IDisposable
{
    private static readonly TimeSpan _hideDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan _entranceFrameInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan _animationInterval = TimeSpan.FromMilliseconds(50);

    private readonly ISettingsStore _settingsStore;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly ILocalizationService _localization;
    private readonly ISystemDoubleClickTime _doubleClickTime;
    private readonly TimeProvider _timeProvider;
    private readonly PetAnimationStateMachine _stateMachine;
    private readonly Serilog.ILogger _logger;
    private readonly Stopwatch _entranceClock = new();
    private readonly Dictionary<PetState, Bitmap[]> _frames = [];
    private readonly HashSet<PetState> _failedFrameStates = [];

    private MainWindow? _shell;
    private PetWindow? _window;
    private DispatcherTimer? _entranceTimer;
    private PetEntrancePath _entrancePath;
    private bool _entranceMirrored;
    private DispatcherTimer? _hideTimer;
    private DispatcherTimer? _animationTimer;
    private DispatcherTimer? _heartDelayTimer;
    private DispatcherTimer? _heartTimer;
    private MenuFlyout? _menuFlyout;
    private PetState? _renderedState;
    private int _renderedFrame = -1;
    private DateTimeOffset? _lastClickAt;
    private bool _shuttingDown;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetWindowController"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies and stores the enabled flag and position.</param>
    /// <param name="errorReporter">Receives failed settings saves.</param>
    /// <param name="recordFactory">Creates those error records.</param>
    /// <param name="localization">Supplies the tooltip, menu and accessible name.</param>
    /// <param name="doubleClickTime">Supplies the operating system's double-click interval.</param>
    /// <param name="timeProvider">Supplies the current time for click gestures.</param>
    /// <param name="stateMachine">Owns the animation state and its timers.</param>
    /// <param name="logger">Receives the pet lifecycle decisions and degradation warnings.</param>
    public PetWindowController(
        ISettingsStore settingsStore,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        ILocalizationService localization,
        ISystemDoubleClickTime doubleClickTime,
        TimeProvider timeProvider,
        PetAnimationStateMachine stateMachine,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(doubleClickTime);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(stateMachine);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _localization = localization;
        _doubleClickTime = doubleClickTime;
        _timeProvider = timeProvider;
        _stateMachine = stateMachine;
        _logger = logger;

        _localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>
    /// Raised after the enabled state changed, from the settings switch, the tray item or the pet itself.
    /// </summary>
    public event EventHandler? EnabledChanged;

    /// <summary>
    /// Raised when a double click asks for the main window; the shell restores and activates it.
    /// </summary>
    public event EventHandler? OpenRequested;

    /// <summary>
    /// Raised when the pet menu asks for the settings page.
    /// </summary>
    public event EventHandler? SettingsRequested;

    /// <summary>
    /// Gets a value indicating whether the desktop pet is enabled.
    /// </summary>
    public bool IsEnabled => _settingsStore.Current.PetEnabled;

    /// <summary>
    /// Attaches the main window: screen changes are observed through it and an enabled pet enters now.
    /// </summary>
    /// <param name="shell">The visible main window.</param>
    public void AttachShell(MainWindow shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_shell is not null)
        {
            return;
        }

        _shell = shell;
        shell.Closed += OnShellClosed;
        shell.Screens.Changed += OnScreensChanged;

        if (IsEnabled)
        {
            // The content viewport has no bounds until the shell's first layout pass has run.
            Dispatcher.UIThread.Post(ShowPet, DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Enables or disables the pet, persists the flag and applies the change immediately.
    /// </summary>
    /// <param name="enabled">Whether the pet should be shown.</param>
    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (enabled == IsEnabled)
        {
            return;
        }

        SavePetEnabled(enabled);
        _logger.Debug("Desktop pet {State}", enabled ? "enabled" : "disabled");

        if (enabled)
        {
            ShowPet();
        }
        else
        {
            HidePet();
        }

        EnabledChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.PropertyChanged -= OnLocalizationChanged;
        if (_shell is { } shell)
        {
            shell.Closed -= OnShellClosed;
            shell.Screens.Changed -= OnScreensChanged;
        }

        CloseWindow();
    }

    private void ShowPet()
    {
        CancelHide();
        if (!IsEnabled)
        {
            return;
        }

        if (_shell is null)
        {
            _logger.Debug("Desktop pet enabled before the shell was ready");
            return;
        }

        PetWindow? window = EnsureWindow();
        if (window is null)
        {
            return;
        }

        IReadOnlyList<PetWorkArea> workAreas = ReadWorkAreas();
        if (workAreas.Count == 0)
        {
            _logger.Warning("The desktop pet found no screen work area and stays hidden");
            return;
        }

        // The entrance and the resting frame are required before anything becomes visible; a missing
        // frame degrades to an invisible pet with the enabled flag kept.
        if (GetFrames(PetState.Idle) is null || GetFrames(PetState.WalkOut) is null)
        {
            return;
        }

        PetPlacementResult landing = ResolveLanding(workAreas);
        PetPoint start = ResolveStartPoint(landing);
        PetEntrancePath path = PetPlacement.CreateEntrancePath(
            start,
            landing.Position,
            landing.WorkArea.Scaling);

        // Show invisible first: a window that cannot be transparent must never paint an opaque box.
        window.Transitions = null;
        window.Opacity = 0d;
        if (!window.IsVisible)
        {
            window.Show();
        }

        window.Position = new PixelPoint(start.X, start.Y);
        if (window.ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
        {
            // The enabled flag stays on; the user can retry from the switch or the tray item.
            _logger.Warning("The desktop pet requires a transparent window and stays hidden");
            window.Hide();
            return;
        }

        window.Opacity = 1d;
        _stateMachine.Show();
        _stateMachine.BeginEntrance();
        _renderedState = null;
        _renderedFrame = -1;
        StartAnimation();
        StartEntrance(window, path);
        Render();
    }

    private void HidePet()
    {
        StopEntrance();
        StopAnimation();
        _lastClickAt = null;
        CancelHeartDelay();
        HideHeart();
        _menuFlyout?.Hide();
        _stateMachine.Hide();
        if (_window is not { } window)
        {
            return;
        }

        window.SetSleeping(false);
        if (!window.IsVisible)
        {
            return;
        }

        window.Transitions = new Transitions { CreateFade() };
        window.Opacity = 0d;
        _hideTimer = new DispatcherTimer { Interval = _hideDuration };
        _hideTimer.Tick += OnHideTick;
        _hideTimer.Start();
    }

    private PetPlacementResult ResolveLanding(IReadOnlyList<PetWorkArea> workAreas)
    {
        AppSettings settings = _settingsStore.Current;
        return settings.PetX is int x && settings.PetY is int y
            ? PetPlacement.Resolve(new PetPoint(x, y), workAreas)
            : PetPlacement.GetDefault(workAreas);
    }

    private PetPoint ResolveStartPoint(PetPlacementResult landing)
    {
        if (_shell is { IsVisible: true } shell &&
            shell.WindowState != WindowState.Minimized &&
            shell.TryGetContentBottomRight(out PixelPoint corner))
        {
            // The cat's bottom-right starts on the content corner and walks out from there.
            return new PetPoint(corner.X - landing.PixelSize, corner.Y - landing.PixelSize);
        }

        return PetPlacement.GetScreenEdgeStart(landing.WorkArea, landing.Position, landing.PixelSize);
    }

    private void StartEntrance(PetWindow window, PetEntrancePath path)
    {
        StopEntrance();
        _entranceMirrored = path.End.X > path.Start.X;
        window.SetMirrored(_entranceMirrored);
        if (path.Duration <= TimeSpan.Zero)
        {
            window.Position = new PixelPoint(path.End.X, path.End.Y);
            _stateMachine.CompleteEntrance();
            StopMirror();
            Render();
            return;
        }

        _entrancePath = path;
        _entranceClock.Restart();
        _entranceTimer = new DispatcherTimer { Interval = _entranceFrameInterval };
        _entranceTimer.Tick += OnEntranceTick;
        _entranceTimer.Start();
    }

    private void OnEntranceTick(object? sender, EventArgs e)
    {
        if (_window is not { IsVisible: true } window)
        {
            StopEntrance();
            return;
        }

        double progress = _entranceClock.Elapsed.TotalSeconds / _entrancePath.Duration.TotalSeconds;
        if (progress >= 1d)
        {
            window.Position = new PixelPoint(_entrancePath.End.X, _entrancePath.End.Y);
            StopEntrance();
            _stateMachine.CompleteEntrance();
            Render();
            return;
        }

        PetPoint position = _entrancePath.PointAt(progress);
        window.Position = new PixelPoint(position.X, position.Y);
    }

    private void StopEntrance()
    {
        StopMirror();
        if (_entranceTimer is null)
        {
            return;
        }

        _entranceClock.Stop();
        _entranceTimer.Stop();
        _entranceTimer.Tick -= OnEntranceTick;
        _entranceTimer = null;
    }

    private void StopMirror()
    {
        if (!_entranceMirrored)
        {
            return;
        }

        _entranceMirrored = false;
        _window?.SetMirrored(false);
    }

    private void StartAnimation()
    {
        StopAnimation();
        _animationTimer = new DispatcherTimer { Interval = _animationInterval };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_animationTimer is null)
        {
            return;
        }

        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer = null;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        _stateMachine.Update();
        Render();
    }

    private void Render()
    {
        if (_window is not { IsVisible: true } window)
        {
            return;
        }

        PetState state = _stateMachine.State;
        int frameIndex = _stateMachine.FrameIndex;
        window.SetSleeping(state == PetState.Sleep);
        if (_renderedState == state && _renderedFrame == frameIndex)
        {
            return;
        }

        Bitmap[]? frames = GetFrames(state);
        if (frames is null)
        {
            HidePet();
            return;
        }

        window.SetFrame(frames[frameIndex]);
        _renderedState = state;
        _renderedFrame = frameIndex;
    }

    private void OnHideTick(object? sender, EventArgs e)
    {
        CancelHide();
        if (_window is not { } window)
        {
            return;
        }

        window.Hide();
        window.Transitions = null;
        window.Opacity = 1d;
    }

    private void CancelHide()
    {
        if (_hideTimer is null)
        {
            return;
        }

        _hideTimer.Stop();
        _hideTimer.Tick -= OnHideTick;
        _hideTimer = null;
    }

    private void OnPetInteractionStarted(object? sender, EventArgs e)
    {
        // A press interrupts the entrance immediately; the release decides between click and drag.
        StopEntrance();
        _stateMachine.InterruptEntrance();
        Render();
    }

    private void OnPetDragStarted(object? sender, EventArgs e)
    {
        _lastClickAt = null;
        CancelHeartDelay();
        HideHeart();
        _stateMachine.BeginDrag();
        Render();
    }

    private void OnPetClicked(object? sender, EventArgs e)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_lastClickAt is { } last)
        {
            TimeSpan sinceLast = now - last;
            if (sinceLast >= TimeSpan.Zero && sinceLast <= _doubleClickTime.DoubleClickInterval)
            {
                _lastClickAt = null;
                CancelHeartDelay();
                HideHeart();
                _stateMachine.DoubleClick();
                Render();
                OpenRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        _lastClickAt = now;
        CancelHeartDelay();
        HideHeart();

        // The reaction starts at once; only the heart waits for the click delay.
        PetClickEffect effect = _stateMachine.Click();
        if (effect == PetClickEffect.React)
        {
            _heartDelayTimer = new DispatcherTimer { Interval = PetInteractionRules.HeartDelay };
            _heartDelayTimer.Tick += OnHeartDelayTick;
            _heartDelayTimer.Start();
        }

        Render();
    }

    private void OnHeartDelayTick(object? sender, EventArgs e)
    {
        CancelHeartDelay();
        ShowHeart();
    }

    private void CancelHeartDelay()
    {
        if (_heartDelayTimer is null)
        {
            return;
        }

        _heartDelayTimer.Stop();
        _heartDelayTimer.Tick -= OnHeartDelayTick;
        _heartDelayTimer = null;
    }

    private void ShowHeart()
    {
        if (_window is not { } window)
        {
            return;
        }

        StopHeart();
        window.SetHeartVisible(true);
        _heartTimer = new DispatcherTimer { Interval = PetInteractionRules.HeartDuration };
        _heartTimer.Tick += OnHeartTick;
        _heartTimer.Start();
    }

    private void OnHeartTick(object? sender, EventArgs e) => HideHeart();

    private void HideHeart()
    {
        StopHeart();
        _window?.SetHeartVisible(false);
    }

    private void StopHeart()
    {
        if (_heartTimer is null)
        {
            return;
        }

        _heartTimer.Stop();
        _heartTimer.Tick -= OnHeartTick;
        _heartTimer = null;
    }

    private void OnPetContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        // The flyout opens after this event; take the pet out of walking or sleep first.
        StopEntrance();
        _lastClickAt = null;
        CancelHeartDelay();
        HideHeart();
        _stateMachine.RightClick();
        Render();

        if (_window is { } window && _menuFlyout is { } menu)
        {
            menu.ShowAt(window);
            e.Handled = true;
        }
    }

    private void OnPetDragCompleted(object? sender, EventArgs e)
    {
        _stateMachine.EndDrag();
        Render();
        if (ClampWindowPosition() is not { } position)
        {
            return;
        }

        SavePetPosition(position);
    }

    private void OnScreensChanged(object? sender, EventArgs e)
    {
        StopEntrance();
        _stateMachine.InterruptEntrance();
        Render();
        _ = ClampWindowPosition();
    }

    private PetPoint? ClampWindowPosition()
    {
        if (_window is not { IsVisible: true } window)
        {
            return null;
        }

        IReadOnlyList<PetWorkArea> workAreas = ReadWorkAreas();
        if (workAreas.Count == 0)
        {
            return null;
        }

        PetPlacementResult placement = PetPlacement.Resolve(
            new PetPoint(window.Position.X, window.Position.Y),
            workAreas);
        window.Position = new PixelPoint(placement.Position.X, placement.Position.Y);
        return placement.Position;
    }

    private void OnPetWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_shuttingDown ||
            e.CloseReason is WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown)
        {
            return;
        }

        // Alt+F4 and the system close mean "hide the pet", like the tray item and the settings switch.
        e.Cancel = true;
        SetEnabled(false);
    }

    private void OnShellClosed(object? sender, EventArgs e)
    {
        if (_shell is { } shell)
        {
            shell.Closed -= OnShellClosed;
            shell.Screens.Changed -= OnScreensChanged;
        }

        CloseWindow();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ILocalizationService.CurrentLanguage))
        {
            return;
        }

        _menuFlyout?.Hide();
        ApplyLocalizedMenu();
    }

    private void ApplyLocalizedMenu()
    {
        var hideItem = new MenuItem { Header = _localization["Shell.Pet.Hide"] };
        hideItem.Click += (_, _) => SetEnabled(false);
        var settingsItem = new MenuItem { Header = _localization["Shell.Pet.Settings"] };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var menu = new MenuFlyout { Placement = PlacementMode.Pointer };
        menu.Items.Add(hideItem);
        menu.Items.Add(settingsItem);
        _menuFlyout = menu;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Window and asset creation are UI boundaries; a missing pet must never take the shell down.")]
    private PetWindow? EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        PetWindow window;
        try
        {
            window = new PetWindow();
        }
        catch (Exception exception)
        {
            _logger.Warning(
                exception,
                "The desktop pet window could not be created; the pet stays hidden");
            return null;
        }

        window.InteractionStarted += OnPetInteractionStarted;
        window.DragStarted += OnPetDragStarted;
        window.Clicked += OnPetClicked;
        window.DragCompleted += OnPetDragCompleted;
        window.ContextRequested += OnPetContextRequested;
        window.Closing += OnPetWindowClosing;
        window.DataContext = _shell?.DataContext;
        _window = window;
        ApplyLocalizedMenu();
        return window;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Asset loading is a UI boundary; a missing frame must degrade to a hidden pet.")]
    private Bitmap[]? GetFrames(PetState state)
    {
        if (_frames.TryGetValue(state, out Bitmap[]? frames))
        {
            return frames;
        }

        if (_failedFrameStates.Contains(state))
        {
            return null;
        }

        PetAnimationClip clip = PetAnimationCatalog.For(state);
        var loaded = new Bitmap[clip.FrameDurations.Count];
        try
        {
            for (int index = 0; index < loaded.Length; index++)
            {
                using Stream stream = AssetLoader.Open(FrameUri(clip.AssetFolder, index));
                loaded[index] = new Bitmap(stream);
            }

            _frames[state] = loaded;
            return loaded;
        }
        catch (Exception exception)
        {
            for (int index = 0; index < loaded.Length; index++)
            {
                loaded[index]?.Dispose();
            }

            _failedFrameStates.Add(state);
            _logger.Warning(
                exception,
                "The desktop pet frames for {State} could not be loaded; the pet stays hidden",
                state);
            return null;
        }
    }

    private static Uri FrameUri(string folder, int index) =>
        new($"avares://JiuMi/Assets/Pet/{folder}/{index.ToString("D2", CultureInfo.InvariantCulture)}.png");

    private IReadOnlyList<PetWorkArea> ReadWorkAreas()
    {
        Screens? screens = _shell?.Screens;
        if (screens is null)
        {
            return [];
        }

        return
        [
            .. screens.All.Select(screen => new PetWorkArea(
                screen.WorkingArea.X,
                screen.WorkingArea.Y,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height,
                screen.Scaling,
                screen.IsPrimary)),
        ];
    }

    private void SavePetEnabled(bool enabled)
    {
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current with
        {
            PetEnabled = enabled,
        });

        if (result.Error is { } error)
        {
            // An explicit user change is visible, like every other settings switch.
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.SettingsSaveFailed,
                "Settings.Pet",
                error));
        }
    }

    private void SavePetPosition(PetPoint position)
    {
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current with
        {
            PetX = position.X,
            PetY = position.Y,
        });

        if (result.Error is { } error)
        {
            // Position is a convenience: log it, skip the notice, and retry on the next drag.
            _logger.Warning(error, "The desktop pet position could not be saved");
        }
    }

    private void CloseWindow()
    {
        _shuttingDown = true;
        StopEntrance();
        StopAnimation();
        CancelHeartDelay();
        StopHeart();
        _menuFlyout?.Hide();
        CancelHide();
        _stateMachine.Hide();
        if (_window is not { } window)
        {
            return;
        }

        _window = null;
        window.InteractionStarted -= OnPetInteractionStarted;
        window.DragStarted -= OnPetDragStarted;
        window.Clicked -= OnPetClicked;
        window.DragCompleted -= OnPetDragCompleted;
        window.ContextRequested -= OnPetContextRequested;
        window.Closing -= OnPetWindowClosing;

        try
        {
            window.Close();
        }
        catch (InvalidOperationException exception)
        {
            // A window that never reached the screen has nothing to close.
            _logger.Debug(exception, "The desktop pet window was already gone");
        }

        foreach (Bitmap[] frames in _frames.Values)
        {
            foreach (Bitmap frame in frames)
            {
                frame.Dispose();
            }
        }

        _frames.Clear();
        _failedFrameStates.Clear();
        _renderedState = null;
        _renderedFrame = -1;
    }

    private static DoubleTransition CreateFade() => new()
    {
        Property = Visual.OpacityProperty,
        Duration = _hideDuration,
    };
}
