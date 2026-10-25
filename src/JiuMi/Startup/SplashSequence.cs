using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using JiuMi.Application.Errors;
using JiuMi.Application.Startup;
using JiuMi.Errors;
using JiuMi.Views;

namespace JiuMi.Startup;

/// <summary>
/// Drives the splash through its stages and hands the application over to the shell window.
/// </summary>
internal sealed class SplashSequence
{
    private static readonly TimeSpan _crossFadeDuration = TimeSpan.FromMilliseconds(200);

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly ShellInitialization _initialization;
    private readonly SplashMedia _media;
    private readonly StartupSession _session;
    private readonly Func<SplashWindow> _createSplashWindow;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly IErrorWindowPresenter _errorWindows;
    private readonly Stopwatch _clock = new();

    private SplashWindow? _splash;
    private MainWindow? _shell;
    private SplashStage _stage = SplashStage.Bootstrapping;
    private DispatcherTimer? _noWindowTimer;
    private bool _mediaDisposed;
    private bool _timeoutReported;
    private bool _lateCompletionReported;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplashSequence"/> class.
    /// </summary>
    /// <param name="desktop">The lifetime that owns the main window and the shutdown rule.</param>
    /// <param name="initialization">The startup work the splash covers.</param>
    /// <param name="media">The resolved splash media.</param>
    /// <param name="createSplashWindow">Builds the splash window.</param>
    /// <param name="errorReporter">Records the startup warnings and late completion.</param>
    /// <param name="recordFactory">Creates those records with the shared environment fields.</param>
    /// <param name="errorWindows">Presents a startup failure that blocks the shell.</param>
    public SplashSequence(
        IClassicDesktopStyleApplicationLifetime desktop,
        ShellInitialization initialization,
        SplashMedia media,
        Func<SplashWindow> createSplashWindow,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        IErrorWindowPresenter errorWindows)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(createSplashWindow);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(errorWindows);

        _desktop = desktop;
        _initialization = initialization;
        _media = media;
        _session = new StartupSession(SplashHandoffGate.ForGif(media.Duration));
        _createSplashWindow = createSplashWindow;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _errorWindows = errorWindows;
    }

    /// <summary>
    /// Puts the splash on screen, or shows the shell directly when no splash window can be built.
    /// </summary>
    public void Start()
    {
        if (TryShowSplash())
        {
            return;
        }

        // Bottom rung: there is no window to fade across, so the work runs silently until the shell
        // can be shown, and a timer keeps the readiness fallback alive without a frame loop.
        DisposeMedia();
        _clock.Restart();
        _stage = SplashStage.Waiting;
        StartInitialization();
        StartNoWindowWatch();
    }

    private bool TryShowSplash()
    {
        try
        {
            SplashWindow splash = _createSplashWindow();
            ApplyMedia(splash);
            splash.Opened += OnSplashOpened;
            _splash = splash;

            // Set before Show: Opened can fire inside Show, and the frame loop only runs from the
            // stage that says the splash is up.
            _stage = SplashStage.SplashShown;
            splash.Show();
            return true;
        }
#pragma warning disable CA1031 // A splash window that cannot be built is the bottom rung: startup carries on without it.
        catch (Exception)
#pragma warning restore CA1031
        {
            if (_splash is { } failed)
            {
                failed.Opened -= OnSplashOpened;
                _splash = null;
            }

            return false;
        }
    }

    private void ApplyMedia(SplashWindow splash)
    {
        if (_media.Animation is { } animation)
        {
            splash.ShowAnimation(animation);
            return;
        }

        if (_media.Poster is { } poster)
        {
            splash.ShowPoster(poster);
            return;
        }

        splash.ShowBackgroundOnly();
    }

    private void OnSplashOpened(object? sender, EventArgs e)
    {
        // The clock starts on the frame after Opened. Timing from window creation would let the first
        // paint eat the beginning of the animation.
        RequestFrame();
    }

    private void OnAnimationFrame(SplashWindow splash)
    {
        if (_stage == SplashStage.SplashShown)
        {
            // Initialization now runs beside the animation instead of before the clock: the splash
            // keeps drawing while the work awaits, so the readiness timeout can be observed at all.
            _clock.Restart();
            _stage = _media.Animation is null ? SplashStage.Waiting : SplashStage.Animating;
            StartInitialization();
        }

        TimeSpan elapsed = _clock.Elapsed;

        // One pass is timed by the application: the GIF control has no way to stop after a single
        // play, so its source is dropped when the measured length is up.
        if (_stage == SplashStage.Animating && elapsed >= _media.Duration)
        {
            // The picture and the ring swap inside this one frame, so nothing is drawn in between.
            splash.StopAnimation();
            _stage = SplashStage.Waiting;
        }

        if (_session.HasFaulted)
        {
            return;
        }

        switch (_session.Evaluate(elapsed))
        {
            case SplashHandoffDecision.Handoff:
                // Readiness is set by the same step that builds the window, which the compiler cannot see.
                TryBeginHandoff(splash, _shell!);
                return;

            case SplashHandoffDecision.ForcedHandoff:
                ReportTimeout();

                // The fallback still needs a window. Building it late may pull in services whose steps
                // are still running; the steps then complete into the already-visible shell.
                if (TryEnsureShell(out MainWindow? fallbackShell))
                {
                    TryBeginHandoff(splash, fallbackShell);
                }

                return;

            default:
                RequestFrame();
                return;
        }
    }

    private void RequestFrame()
    {
        // The stage is what ends the loop: once the handoff begins, no further frame is queued, and a
        // request is only ever pending one tick at a time.
        if (!IsMeasuring || _splash is not { } splash)
        {
            return;
        }

        splash.RequestAnimationFrame(_ => OnAnimationFrame(splash));
    }

    private bool IsMeasuring =>
        _stage is SplashStage.SplashShown or SplashStage.Animating or SplashStage.Waiting;

    private bool HasLeftSplash =>
        _stage is SplashStage.Handoff or SplashStage.Running or SplashStage.Faulted;

    private void StartInitialization()
    {
        // The frame loop owns the interface, so the task is not awaited here. InitializeAsync catches
        // every failure and posts the outcome, so the discarded task cannot fault.
        _ = InitializeAsync();
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Startup is a boundary: any failed step must turn into the Faulted error window instead of escaping the frame loop.")]
    private async Task InitializeAsync()
    {
        try
        {
            await _initialization.RunAsync();
        }
        catch (Exception exception)
        {
            Dispatcher.UIThread.Post(() => Fault(ErrorCodes.StartupFaulted, "Startup.Initialize", exception));
            return;
        }

        Dispatcher.UIThread.Post(OnInitializationCompleted);
    }

    private void OnInitializationCompleted()
    {
        if (_session.HasFaulted || !TryEnsureShell(out MainWindow? shell))
        {
            return;
        }

        bool completedLate = _session.HasTimedOut;
        _session.MarkReady();
        if (completedLate)
        {
            // The splash left at the timeout; the theme and language just applied are the late results
            // the architecture promises to keep.
            ReportLateCompletion();
        }

        if (HasLeftSplash)
        {
            return;
        }

        if (_splash is null)
        {
            ShowShellDirectly(shell);
            return;
        }

        RequestFrame();
    }

    private bool TryEnsureShell([NotNullWhen(true)] out MainWindow? shell)
    {
        if (_shell is not null)
        {
            shell = _shell;
            return true;
        }

        MainWindow? created = null;
        bool succeeded = TryShellOperation("Shell.MainWindow.Create", () =>
        {
            created = _initialization.CreateMainWindow();

            // Assigned before any handoff: once the shutdown rule is OnMainWindowClose, closing the splash
            // after this point must not take the process down with it.
            _desktop.MainWindow = created;
            _shell = created;
        });

        shell = succeeded ? created : null;
        return succeeded;
    }

    private void ShowShellDirectly(MainWindow shell)
    {
        if (HasLeftSplash)
        {
            return;
        }

        _stage = SplashStage.Handoff;
        StopNoWindowWatch();

        if (!TryShellOperation("Shell.MainWindow.Show", () =>
            {
                shell.Show();
                shell.Activate();
            }))
        {
            return;
        }

        _stage = SplashStage.Running;
    }

    private void TryBeginHandoff(SplashWindow splash, MainWindow shell) =>
        _ = TryShellOperation("Shell.MainWindow.Show", () => BeginHandoff(splash, shell));

    private void BeginHandoff(SplashWindow splash, MainWindow shell)
    {
        _stage = SplashStage.Handoff;
        _clock.Stop();

        shell.Opacity = 0d;
        shell.Show();

        // The shell paints above the splash at zero opacity, so no Topmost is involved: the splash
        // shows through while both windows run the same curve.
        shell.Transitions = new Transitions { CreateFade() };
        splash.Transitions = new Transitions { CreateFade() };

        // The fade starts from a painted frame at zero opacity; changing the value in the same tick as
        // Show would have nothing to interpolate from. The countdown rides along with that first value
        // change, otherwise it would spend part of the 200ms before either window began to fade.
        shell.RequestAnimationFrame(_ =>
        {
            shell.Opacity = 1d;
            splash.Opacity = 0d;

            var crossFade = new DispatcherTimer { Interval = _crossFadeDuration };
            crossFade.Tick += (_, _) =>
            {
                crossFade.Stop();
                FinishHandoff(splash, shell);
            };
            crossFade.Start();
        });
    }

    private void FinishHandoff(SplashWindow splash, MainWindow shell)
    {
        _ = TryShellOperation("Shell.MainWindow.Activate", () =>
        {
            shell.Opacity = 1d;

            // The main window is assigned by now, so this close leaves the process alone.
            splash.Opened -= OnSplashOpened;
            _splash = null;
            splash.Close();
            DisposeMedia();

            shell.Activate();
            _stage = SplashStage.Running;
        });
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Showing or activating the shell is a UI boundary; its failure is presented, not rethrown.")]
    private bool TryShellOperation(string operation, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception)
        {
            Fault(ErrorCodes.ShellMainWindowFailed, operation, exception);
            return false;
        }
    }

    private void StartNoWindowWatch()
    {
        _noWindowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _noWindowTimer.Tick += OnNoWindowTick;
        _noWindowTimer.Start();
    }

    private void OnNoWindowTick(object? sender, EventArgs e)
    {
        if (_session.HasFaulted)
        {
            StopNoWindowWatch();
            return;
        }

        SplashHandoffDecision decision = _session.Evaluate(_clock.Elapsed);
        if (decision != SplashHandoffDecision.ForcedHandoff)
        {
            return;
        }

        ReportTimeout();
        if (TryEnsureShell(out MainWindow? shell))
        {
            ShowShellDirectly(shell);
        }
    }

    private void StopNoWindowWatch()
    {
        if (_noWindowTimer is null)
        {
            return;
        }

        _noWindowTimer.Stop();
        _noWindowTimer.Tick -= OnNoWindowTick;
        _noWindowTimer = null;
    }

    private void ReportTimeout()
    {
        if (_timeoutReported)
        {
            return;
        }

        _timeoutReported = true;
        _errorReporter.Report(_recordFactory.Create(ErrorCodes.StartupTimeout, "Startup.Initialize"));
    }

    private void ReportLateCompletion()
    {
        if (_lateCompletionReported)
        {
            return;
        }

        _lateCompletionReported = true;
        _errorReporter.Report(_recordFactory.Create(ErrorCodes.StartupLateCompletion, "Startup.Initialize"));
    }

    private void Fault(string code, string operation, Exception exception)
    {
        if (_stage == SplashStage.Faulted)
        {
            return;
        }

        _stage = SplashStage.Faulted;
        _session.MarkFaulted();
        _clock.Stop();
        StopNoWindowWatch();
        _splash?.StopAnimation();
        DisposeMedia();

        // The presenter records the failure and decides the blocking variant from the main window.
        _errorWindows.PresentFailure(code, operation, exception);
        CloseSplash();
    }

    private void CloseSplash()
    {
        if (_splash is not { } splash)
        {
            return;
        }

        _splash = null;
        splash.Opened -= OnSplashOpened;

#pragma warning disable CA1031 // The error window is already the user's surface; closing the splash must not replace it with a crash.
        try
        {
            splash.Close();
        }
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }

    private void DisposeMedia()
    {
        if (_mediaDisposed)
        {
            return;
        }

        _mediaDisposed = true;
        _media.Dispose();
    }

    private static DoubleTransition CreateFade() =>
        new()
        {
            Property = Visual.OpacityProperty,
            Duration = _crossFadeDuration,

            // A pure exit: quick off the mark, settling at the end, which is how the Fluent fade reads.
            Easing = new CubicEaseOut(),
        };
}
