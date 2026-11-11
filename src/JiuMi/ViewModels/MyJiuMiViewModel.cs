using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using JiuMi.Application.MyJiuMi;
using JiuMi.Application.Settings;

namespace JiuMi.ViewModels;

/// <summary>
/// Drives the My JiuMi page: the state machine, the decoded frames, the heart and <c>Zzz</c>
/// overlays, and the persisted normalized floor position.
/// </summary>
public sealed class MyJiuMiViewModel : ViewModelBase, IDisposable
{
    private static readonly Uri _assetRoot = new("avares://JiuMi/Assets/MyJiuMi/");

    private readonly ISettingsStore _settingsStore;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;
    private readonly MyJiuMiStateMachine _stateMachine;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<MyJiuMiState, Bitmap?[]> _frames = [];
    private readonly HashSet<MyJiuMiState> _failedStates = [];

    private Bitmap? _frame;
    private Thickness _catMargin;
    private bool _isCatVisible = true;
    private bool _isHeartVisible;
    private bool _isZzzVisible;
    private bool _isFacingRight;
    private bool _isDisposed;
    private double _stageWidth;
    private DateTimeOffset? _heartRevealAt;
    private DateTimeOffset? _heartHideAt;
    private MyJiuMiState? _renderedState;
    private int _renderedFrame = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyJiuMiViewModel"/> class.
    /// </summary>
    /// <param name="settingsStore">Supplies and stores the normalized cat position.</param>
    /// <param name="timeProvider">Supplies the animation clock.</param>
    /// <param name="logger">Receives frame-loading and position-save warnings.</param>
    public MyJiuMiViewModel(
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
        _stateMachine = new MyJiuMiStateMachine(timeProvider, Random.Shared);
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
    }

    /// <summary>
    /// Gets the frame to draw, or <see langword="null"/> when the state's frames are unavailable.
    /// </summary>
    public Bitmap? Frame
    {
        get => _frame;
        private set => SetProperty(ref _frame, value);
    }

    /// <summary>
    /// Gets the cat's margin inside the room wall; the cat sits on the floor line.
    /// </summary>
    public Thickness CatMargin
    {
        get => _catMargin;
        private set => SetProperty(ref _catMargin, value);
    }

    /// <summary>
    /// Gets the cat centre's horizontal offset from the room wall's left edge, in DIP.
    /// </summary>
    public double CatCenterX => _catMargin.Left + (MyJiuMiInteractionRules.CatSize / 2);

    /// <summary>
    /// Gets a value indicating whether the cat has frames to show.
    /// </summary>
    public bool IsCatVisible
    {
        get => _isCatVisible;
        private set => SetProperty(ref _isCatVisible, value);
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
    /// Gets a value indicating whether the page is currently running the cat.
    /// </summary>
    public bool IsActive => _stateMachine.IsActive;

    /// <summary>
    /// Starts a fresh page visit from <see cref="MyJiuMiState.LyingIdle"/> at the saved position.
    /// </summary>
    /// <param name="stageWidth">The room width in DIP.</param>
    public void Activate(double stageWidth)
    {
        if (_isDisposed || !(stageWidth > 0))
        {
            return;
        }

        _stageWidth = stageWidth;
        ClearHeart();
        double position = MyJiuMiPositionRules.Clamp(_settingsStore.Current.MyJiuMi.X);
        _stateMachine.Activate(stageWidth, position);
        _timer.Start();
        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Stops the timer and resets the overlays when the page is left or the window is hidden.
    /// </summary>
    public void Deactivate()
    {
        _timer.Stop();
        _stateMachine.Deactivate();
        ClearHeart();
        IsHeartVisible = false;
        IsZzzVisible = false;
        _renderedState = null;
        _renderedFrame = -1;
    }

    /// <summary>
    /// Releases the decoded frames when the application shuts down.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Deactivate();
        Frame = null;

        foreach (Bitmap?[] frames in _frames.Values)
        {
            foreach (Bitmap? frame in frames)
            {
                frame?.Dispose();
            }
        }

        _frames.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Recomputes the cat's pixel position after the room is resized. The stored normalized position
    /// is not changed.
    /// </summary>
    /// <param name="stageWidth">The new room width in DIP.</param>
    public void SetStageWidth(double stageWidth)
    {
        if (_isDisposed || !(stageWidth > 0))
        {
            return;
        }

        _stageWidth = stageWidth;
        _stateMachine.SetStageWidth(stageWidth);
        UpdateCatPosition();
    }

    /// <summary>
    /// Applies a click on the cat and schedules the heart when the cat reacted.
    /// </summary>
    public void Click()
    {
        MyJiuMiClickEffect effect = _stateMachine.Click();
        if (effect == MyJiuMiClickEffect.React)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            _heartRevealAt = now + MyJiuMiInteractionRules.HeartDelay;
            _heartHideAt = _heartRevealAt + MyJiuMiInteractionRules.HeartDuration;
        }
        else
        {
            ClearHeart();
        }

        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Starts dragging the cat; a sleeping cat is picked up directly.
    /// </summary>
    public void BeginDrag()
    {
        ClearHeart();
        IsHeartVisible = false;
        _stateMachine.BeginDrag();
        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Moves the cat to the pointer's normalized horizontal position.
    /// </summary>
    /// <param name="positionX">The normalized pointer position.</param>
    public void DragTo(double positionX)
    {
        _stateMachine.SetPosition(positionX);
        UpdateCatPosition();
    }

    /// <summary>
    /// Ends the drag, saves the normalized position and settles back on the floor.
    /// </summary>
    public void EndDrag()
    {
        _stateMachine.EndDrag();
        SavePosition();
        UpdateVisuals(forceFrame: true);
    }

    /// <summary>
    /// Summons the cat to the double-clicked floor position.
    /// </summary>
    /// <param name="positionX">The normalized click position.</param>
    public void Summon(double positionX)
    {
        _stateMachine.Summon(positionX);
        UpdateVisuals(forceFrame: true);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _stateMachine.Update();
        UpdateVisuals(forceFrame: false);
    }

    private void UpdateVisuals(bool forceFrame)
    {
        if (_isDisposed)
        {
            return;
        }

        MyJiuMiState state = _stateMachine.State;
        int frameIndex = _stateMachine.FrameIndex;
        if (forceFrame || _renderedState != state || _renderedFrame != frameIndex)
        {
            Bitmap?[]? frames = GetFrames(state);
            if (frames is null || frameIndex >= frames.Length)
            {
                IsCatVisible = false;
                Frame = null;
            }
            else
            {
                IsCatVisible = true;
                Frame = frames[frameIndex];
            }

            _renderedState = state;
            _renderedFrame = frameIndex;
        }

        IsFacingRight = _stateMachine.IsFacingRight;
        UpdateCatPosition();

        DateTimeOffset now = _timeProvider.GetUtcNow();
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

    private void UpdateCatPosition()
    {
        double width = _stageWidth;
        double catSize = MyJiuMiInteractionRules.CatSize;
        double left = 0;
        if (width > catSize)
        {
            left = Math.Clamp(
                (_stateMachine.PositionX * width) - (catSize / 2),
                0,
                width - catSize);
        }

        CatMargin = new Thickness(left, 0, 0, 0);
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
                X = MyJiuMiPositionRules.Clamp(_stateMachine.PositionX),
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
}
