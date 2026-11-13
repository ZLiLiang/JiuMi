using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media.Imaging;
using JiuMi.Application.MyJiuMi;
using JiuMi.MyJiuMi;

namespace JiuMi.ViewModels;

/// <summary>
/// Adapts the shared <see cref="MyJiuMiSession"/> to the My JiuMi page: the cat margin inside the
/// room wall and the empty room shown while the cat is on the desktop.
/// </summary>
public sealed class MyJiuMiViewModel : ViewModelBase, IDisposable
{
    private readonly MyJiuMiSession _session;
    private double _stageWidth;
    private Thickness _catMargin;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyJiuMiViewModel"/> class.
    /// </summary>
    /// <param name="session">The shared session that owns the state machine and frames.</param>
    public MyJiuMiViewModel(MyJiuMiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <summary>
    /// Gets the session shared with the desktop window.
    /// </summary>
    public MyJiuMiSession Session => _session;

    /// <summary>
    /// Gets the frame to draw, or <see langword="null"/> when the state's frames are unavailable.
    /// </summary>
    public Bitmap? Frame => _session.Frame;

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
    /// Gets a value indicating whether the cat host is shown in the page; it is hidden while the cat
    /// lives on the desktop.
    /// </summary>
    public bool IsCatRootVisible => _session.Presence != MyJiuMiPresence.Desktop;

    /// <summary>
    /// Gets a value indicating whether the cat has frames to show in the page.
    /// </summary>
    public bool IsCatVisible => IsCatRootVisible && _session.IsFrameVisible;

    /// <summary>
    /// Gets a value indicating whether the heart overlay is showing.
    /// </summary>
    public bool IsHeartVisible => _session.IsHeartVisible;

    /// <summary>
    /// Gets a value indicating whether the <c>Zzz</c> overlay is showing.
    /// </summary>
    public bool IsZzzVisible => _session.IsZzzVisible;

    /// <summary>
    /// Gets a value indicating whether the walking frames should be mirrored to face right.
    /// </summary>
    public bool IsFacingRight => _session.IsFacingRight;

    /// <summary>
    /// Gets the cat's scale while it walks out of the page.
    /// </summary>
    public double HomeScale => _session.HomeScale;

    /// <summary>
    /// Gets the procedural micro-motion of the page cat.
    /// </summary>
    public MyJiuMiMotion Motion => _session.Motion;

    /// <summary>
    /// Gets a value indicating whether the page is currently running the cat.
    /// </summary>
    public bool IsActive => IsCatRootVisible && _session.IsMachineActive;

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
        _session.ActivateHome(stageWidth);
        UpdateCatPosition();
    }

    /// <summary>
    /// Stops the timer and resets the overlays when the page is left or the window is hidden.
    /// </summary>
    public void Deactivate()
    {
        _session.DeactivateHome();
        UpdateCatPosition();
    }

    /// <summary>
    /// Releases the subscription when the application shuts down.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _session.PropertyChanged -= OnSessionPropertyChanged;
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
        _session.SetHomeStageWidth(stageWidth);
        UpdateCatPosition();
    }

    /// <summary>
    /// Applies a click on the cat.
    /// </summary>
    public void Click() => _session.Click();

    /// <summary>
    /// Starts dragging the cat; a sleeping cat is picked up directly.
    /// </summary>
    public void BeginDrag() => _session.BeginHomeDrag();

    /// <summary>
    /// Moves the cat to the pointer's normalized horizontal position.
    /// </summary>
    /// <param name="positionX">The normalized pointer position.</param>
    public void DragTo(double positionX) => _session.DragTo(positionX);

    /// <summary>
    /// Ends the drag, saves the normalized position and settles back on the floor.
    /// </summary>
    public void EndDrag() => _session.EndHomeDrag();

    /// <summary>
    /// Hands the dragged cat to the desktop at its bottom-centre in physical screen pixels.
    /// </summary>
    /// <param name="anchor">The cat's bottom-centre in physical screen pixels.</param>
    /// <returns><see langword="true"/> when the desktop host took over.</returns>
    public bool TryHandOffToDesktop(PixelPoint anchor) => _session.TryDragOut(anchor);

    /// <summary>
    /// Shows the temporary desktop preview that keeps the dragged cat visible outside the window.
    /// </summary>
    /// <param name="anchor">The cat's bottom-centre in physical screen pixels.</param>
    /// <returns><see langword="true"/> when the preview is on screen.</returns>
    public bool ShowDragPreview(PixelPoint anchor) => _session.ShowDragPreview(anchor);

    /// <summary>
    /// Moves the drag preview so its bottom-centre follows <paramref name="anchor"/>.
    /// </summary>
    /// <param name="anchor">The cat's bottom-centre in physical screen pixels.</param>
    /// <returns><see langword="false"/> when the preview is no longer on screen.</returns>
    public bool MoveDragPreview(PixelPoint anchor) => _session.MoveDragPreview(anchor);

    /// <summary>
    /// Closes the drag preview when the cat returns inside the window.
    /// </summary>
    public void CloseDragPreview() => _session.CloseDragPreview();

    /// <summary>
    /// Summons the cat to the double-clicked floor position.
    /// </summary>
    /// <param name="positionX">The normalized click position.</param>
    public void Summon(double positionX) => _session.Summon(positionX);

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MyJiuMiSession.PositionX):
                UpdateCatPosition();
                break;

            case nameof(MyJiuMiSession.Presence):
            case nameof(MyJiuMiSession.IsMachineActive):
                UpdateCatPosition();
                OnPropertyChanged(nameof(IsCatRootVisible));
                OnPropertyChanged(nameof(IsCatVisible));
                OnPropertyChanged(nameof(IsActive));
                break;

            case nameof(MyJiuMiSession.IsFrameVisible):
                OnPropertyChanged(nameof(IsCatVisible));
                break;

            case nameof(MyJiuMiSession.Frame):
                OnPropertyChanged(nameof(Frame));
                break;

            case nameof(MyJiuMiSession.IsHeartVisible):
                OnPropertyChanged(nameof(IsHeartVisible));
                break;

            case nameof(MyJiuMiSession.IsZzzVisible):
                OnPropertyChanged(nameof(IsZzzVisible));
                break;

            case nameof(MyJiuMiSession.IsFacingRight):
                OnPropertyChanged(nameof(IsFacingRight));
                break;

            case nameof(MyJiuMiSession.HomeScale):
                OnPropertyChanged(nameof(HomeScale));
                break;

            case nameof(MyJiuMiSession.Motion):
                OnPropertyChanged(nameof(Motion));
                break;
        }
    }

    private void UpdateCatPosition()
    {
        double width = _stageWidth;
        double catSize = MyJiuMiInteractionRules.CatSize;
        double left = 0;
        if (width > catSize)
        {
            left = Math.Clamp(
                (_session.PositionX * width) - (catSize / 2),
                0,
                width - catSize);
        }

        CatMargin = new Thickness(left, 0, 0, 0);
        OnPropertyChanged(nameof(CatCenterX));
    }
}
