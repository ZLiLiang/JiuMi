using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using JiuMi.Application.Errors;
using JiuMi.Application.Pet;
using JiuMi.Application.Settings;
using JiuMi.Views;

namespace JiuMi.Pet;

/// <summary>
/// Owns the desktop pet window: lazy creation, entrance movement, visibility, dragging and the saved
/// enabled/position state.
/// </summary>
/// <remarks>
/// The controller reads and writes the settings store and turns screen and shell state into a window
/// position. The window itself only renders a frame and forwards pointer drags.
/// </remarks>
public sealed class PetWindowController : IDisposable
{
    private static readonly Uri _placeholderFrameUri =
        new("avares://JiuMi/Assets/Pet/idle/00.png");

    private static readonly TimeSpan _hideDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan _frameInterval = TimeSpan.FromMilliseconds(16);

    private readonly ISettingsStore _settingsStore;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly Serilog.ILogger _logger;
    private readonly Stopwatch _entranceClock = new();

    private MainWindow? _shell;
    private PetWindow? _window;
    private Bitmap? _frame;
    private DispatcherTimer? _entranceTimer;
    private PetEntrancePath _entrancePath;
    private DispatcherTimer? _hideTimer;
    private bool _shuttingDown;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetWindowController"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies and stores the enabled flag and position.</param>
    /// <param name="errorReporter">Receives failed settings saves.</param>
    /// <param name="recordFactory">Creates those error records.</param>
    /// <param name="logger">Receives the pet lifecycle decisions and degradation warnings.</param>
    public PetWindowController(
        ISettingsStore settingsStore,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsStore = settingsStore;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _logger = logger;
    }

    /// <summary>
    /// Raised after the enabled state changed, from the settings switch, the tray item or the pet itself.
    /// </summary>
    public event EventHandler? EnabledChanged;

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
        StartEntrance(window, path);
    }

    private void HidePet()
    {
        StopEntrance();
        CancelHide();
        if (_window is not { IsVisible: true } window)
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
        if (path.Duration <= TimeSpan.Zero)
        {
            window.Position = new PixelPoint(path.End.X, path.End.Y);
            return;
        }

        _entrancePath = path;
        _entranceClock.Restart();
        _entranceTimer = new DispatcherTimer { Interval = _frameInterval };
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
            return;
        }

        PetPoint position = _entrancePath.PointAt(progress);
        window.Position = new PixelPoint(position.X, position.Y);
    }

    private void StopEntrance()
    {
        if (_entranceTimer is null)
        {
            return;
        }

        _entranceClock.Stop();
        _entranceTimer.Stop();
        _entranceTimer.Tick -= OnEntranceTick;
        _entranceTimer = null;
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

    private void OnPetInteractionStarted(object? sender, EventArgs e) => StopEntrance();

    private void OnPetDragCompleted(object? sender, EventArgs e)
    {
        if (ClampWindowPosition() is not { } position)
        {
            return;
        }

        SavePetPosition(position);
    }

    private void OnScreensChanged(object? sender, EventArgs e)
    {
        StopEntrance();
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
            using Stream stream = AssetLoader.Open(_placeholderFrameUri);
            _frame = new Bitmap(stream);
            window.SetFrame(_frame);
        }
        catch (Exception exception)
        {
            _frame?.Dispose();
            _frame = null;
            _logger.Warning(
                exception,
                "The desktop pet window or its frame could not be created; the pet stays hidden");
            return null;
        }

        window.InteractionStarted += OnPetInteractionStarted;
        window.DragCompleted += OnPetDragCompleted;
        window.Closing += OnPetWindowClosing;
        _window = window;
        return window;
    }

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
        CancelHide();
        if (_window is not { } window)
        {
            return;
        }

        _window = null;
        window.InteractionStarted -= OnPetInteractionStarted;
        window.DragCompleted -= OnPetDragCompleted;
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

        _frame?.Dispose();
        _frame = null;
    }

    private static DoubleTransition CreateFade() => new()
    {
        Property = Visual.OpacityProperty,
        Duration = _hideDuration,
    };
}
