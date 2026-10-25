using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using JiuMi.Application.Startup;
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
    private readonly SplashHandoffGate _gate;
    private readonly Func<SplashWindow> _createSplashWindow;
    private readonly Stopwatch _clock = new();

    private SplashWindow? _splash;
    private MainWindow? _shell;
    private SplashStage _stage = SplashStage.Bootstrapping;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplashSequence"/> class.
    /// </summary>
    /// <param name="desktop">The lifetime that owns the main window and the shutdown rule.</param>
    /// <param name="initialization">The startup work the splash covers.</param>
    /// <param name="media">The resolved splash media.</param>
    /// <param name="createSplashWindow">Builds the splash window.</param>
    public SplashSequence(
        IClassicDesktopStyleApplicationLifetime desktop,
        ShellInitialization initialization,
        SplashMedia media,
        Func<SplashWindow> createSplashWindow)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(createSplashWindow);

        _desktop = desktop;
        _initialization = initialization;
        _media = media;
        _gate = SplashHandoffGate.ForGif(media.Duration);
        _createSplashWindow = createSplashWindow;
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

        // Bottom rung: there is no window to fade across, so the handoff degrades into a single show.
        MainWindow shell = CompleteInitialization();
        _media.Dispose();
        shell.Show();
        shell.Activate();
        _stage = SplashStage.Running;
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
        switch (_media.Kind)
        {
            case SplashMediaKind.Animation when _media.Animation is { } animation:
                splash.ShowAnimation(animation);
                break;
            case SplashMediaKind.Poster when _media.Poster is { } poster:
                splash.ShowPoster(poster);
                break;
            default:
                splash.ShowBackgroundOnly();
                break;
        }
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
            _clock.Restart();
            CompleteInitialization();
            _stage = _media.Kind == SplashMediaKind.Animation
                ? SplashStage.Animating
                : SplashStage.Waiting;
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

        if (_gate.IsSatisfied(elapsed, IsInitializationReady))
        {
            // Readiness is set by the same step that builds the window, which the compiler cannot see.
            BeginHandoff(splash, _shell!);
            return;
        }

        RequestFrame();
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

    private MainWindow CompleteInitialization()
    {
        MainWindow shell = _initialization.Run();

        // Assigned before any handoff: once the shutdown rule is OnMainWindowClose, closing the splash
        // after this point must not take the process down with it.
        _desktop.MainWindow = shell;
        _shell = shell;
        return shell;
    }

    private bool IsInitializationReady => _shell is not null;

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
        // Show would have nothing to interpolate from.
        shell.RequestAnimationFrame(_ =>
        {
            shell.Opacity = 1d;
            splash.Opacity = 0d;
        });

        var crossFade = new DispatcherTimer { Interval = _crossFadeDuration };
        crossFade.Tick += (_, _) =>
        {
            crossFade.Stop();
            FinishHandoff(splash, shell);
        };
        crossFade.Start();
    }

    private void FinishHandoff(SplashWindow splash, MainWindow shell)
    {
        shell.Opacity = 1d;

        // The main window is assigned by now, so this close leaves the process alone.
        splash.Opened -= OnSplashOpened;
        _splash = null;
        splash.Close();
        _media.Dispose();

        shell.Activate();
        _stage = SplashStage.Running;
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
