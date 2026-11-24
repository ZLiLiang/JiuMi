using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application.Localization;
using JiuMi.Application.WeChatVideo;

namespace JiuMi.ViewModels;

/// <summary>
/// One captured WeChat video in the session list, with its cover, text, status and primary action.
/// </summary>
public sealed class WeChatVideoRowViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly Action<WeChatVideoRowViewModel> _primaryAction;
    private readonly Action<WeChatVideoRowViewModel> _openVideoAction;
    private readonly Action<WeChatVideoRowViewModel> _revealInFolderAction;
    private readonly Action<WeChatVideoRowViewModel> _redownloadAction;
    private readonly Action<WeChatVideoRowViewModel> _removeAction;
    private readonly Action<WeChatVideoRowViewModel, string?> _selectQualityAction;
    private WeChatVideoItem _item;
    private IReadOnlyList<WeChatVideoSpecOption> _qualityOptions = [];
    private WeChatVideoSpecOption? _selectedQualityOption;
    private string? _actionErrorKey;
    private bool _syncingQualitySelection;
    private bool _canDownload;
    private bool _isLast;
    private bool _isDisposed;
    private Bitmap? _thumbnail;
    private Bitmap? _thumbnailGray;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeChatVideoRowViewModel"/> class.
    /// </summary>
    /// <param name="item">The captured video and its session download state.</param>
    /// <param name="localization">Supplies the row text.</param>
    /// <param name="canDownload">Whether the download folder is usable.</param>
    /// <param name="primaryAction">Runs the action of the row's state button.</param>
    /// <param name="openVideoAction">Opens the downloaded video in the system default player.</param>
    /// <param name="revealInFolderAction">Reveals the downloaded video in the system file manager.</param>
    /// <param name="redownloadAction">Re-downloads a completed or missing-file video.</param>
    /// <param name="removeAction">Removes the video from the session list without deleting local files.</param>
    /// <param name="selectQualityAction">Records the user's quality variant selection for this row.</param>
    public WeChatVideoRowViewModel(
        WeChatVideoItem item,
        ILocalizationService localization,
        bool canDownload,
        Action<WeChatVideoRowViewModel> primaryAction,
        Action<WeChatVideoRowViewModel> openVideoAction,
        Action<WeChatVideoRowViewModel> revealInFolderAction,
        Action<WeChatVideoRowViewModel> redownloadAction,
        Action<WeChatVideoRowViewModel> removeAction,
        Action<WeChatVideoRowViewModel, string?> selectQualityAction)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(primaryAction);
        ArgumentNullException.ThrowIfNull(openVideoAction);
        ArgumentNullException.ThrowIfNull(revealInFolderAction);
        ArgumentNullException.ThrowIfNull(redownloadAction);
        ArgumentNullException.ThrowIfNull(removeAction);
        ArgumentNullException.ThrowIfNull(selectQualityAction);

        _item = item;
        _localization = localization;
        _canDownload = canDownload;
        _primaryAction = primaryAction;
        _openVideoAction = openVideoAction;
        _revealInFolderAction = revealInFolderAction;
        _redownloadAction = redownloadAction;
        _removeAction = removeAction;
        _selectQualityAction = selectQualityAction;
        PrimaryCommand = new RelayCommand(RunPrimaryAction, () => CanPrimaryAction);
        OpenVideoCommand = new RelayCommand(RunOpenVideo, () => CanOpenOrReveal);
        RevealInFolderCommand = new RelayCommand(RunRevealInFolder, () => CanOpenOrReveal);
        RedownloadCommand = new RelayCommand(RunRedownload, () => CanRedownload);
        RemoveCommand = new RelayCommand(RunRemove, () => !_isDisposed);
        RefreshQualityOptions(forceRebuild: true);
    }

    /// <summary>
    /// Gets the captured video and its download state.
    /// </summary>
    public WeChatVideoItem Item => _item;

    /// <summary>
    /// Gets the opaque session key of the video.
    /// </summary>
    public string Key => _item.Key;

    /// <summary>
    /// Gets the cover URL, when the page supplied one.
    /// </summary>
    public string? ThumbnailUrl => _item.Capture.ThumbUrl;

    /// <summary>
    /// Gets the description, or the localized placeholder when it is empty.
    /// </summary>
    public string Description => _item.Capture.Description is { } description &&
        !string.IsNullOrWhiteSpace(description)
        ? description.Trim()
        : _localization["Tool.WeChatVideo.Untitled"];

    /// <summary>
    /// Gets the second line: author, duration and actual size, skipping the missing parts.
    /// </summary>
    public string MetaText => WeChatVideoText.BuildMeta(
        _item.Capture.Uploader,
        _item.Capture.VideoLengthSeconds,
        _item.TotalBytes);

    /// <summary>
    /// Gets a value indicating whether the second line has any text.
    /// </summary>
    public bool HasMeta => MetaText.Length > 0;

    /// <summary>
    /// Gets the status chip text.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (_item.State == WeChatVideoDownloadState.Downloading &&
                _item.TotalBytes is { } total &&
                total > 0)
            {
                int percent = (int)Math.Clamp(_item.DownloadedBytes * 100d / total, 0, 100);
                return _localization.Format("Tool.WeChatVideo.Status.DownloadingPercent", percent);
            }

            if (_item.IsLocalFileMissing)
            {
                return _localization["Tool.WeChatVideo.Status.FileMissing"];
            }

            return _localization[_item.State switch
            {
                WeChatVideoDownloadState.Queued => "Tool.WeChatVideo.Status.Queued",
                WeChatVideoDownloadState.Downloading => "Tool.WeChatVideo.Status.Downloading",
                WeChatVideoDownloadState.Completed => "Tool.WeChatVideo.Status.Completed",
                WeChatVideoDownloadState.Failed => "Tool.WeChatVideo.Status.Failed",
                WeChatVideoDownloadState.Canceled => "Tool.WeChatVideo.Status.Canceled",
                WeChatVideoDownloadState.Expired => "Tool.WeChatVideo.Status.Expired",
                _ => "Tool.WeChatVideo.Status.Idle",
            }];
        }
    }

    /// <summary>
    /// Gets a value indicating whether the video link expired.
    /// </summary>
    public bool IsExpired => _item.State == WeChatVideoDownloadState.Expired;

    /// <summary>
    /// Gets a value indicating whether the video was downloaded.
    /// </summary>
    public bool IsCompleted => _item.State == WeChatVideoDownloadState.Completed;

    /// <summary>
    /// Gets a value indicating whether the previously downloaded local file was detected missing.
    /// </summary>
    public bool IsLocalFileMissing => _item.IsLocalFileMissing;

    /// <summary>
    /// Gets a value indicating whether the row should render the four compact completed-state icon buttons.
    /// </summary>
    public bool ShowCompletedActions => IsCompleted;

    /// <summary>
    /// Gets a value indicating whether the row should render the re-download and remove buttons
    /// after a missing local file was detected.
    /// </summary>
    public bool ShowFileMissingActions => !IsCompleted && IsLocalFileMissing;

    /// <summary>
    /// Gets a value indicating whether the row should render the default single state button.
    /// </summary>
    public bool ShowDefaultPrimaryAction => !IsCompleted && !IsLocalFileMissing;

    /// <summary>
    /// Gets a value indicating whether opening or revealing the local video file is currently allowed.
    /// </summary>
    public bool CanOpenOrReveal => IsCompleted && !string.IsNullOrWhiteSpace(_item.SavedPath);

    /// <summary>
    /// Gets a value indicating whether re-downloading the video is currently allowed.
    /// </summary>
    public bool CanRedownload => _canDownload && (IsCompleted || IsLocalFileMissing);

    /// <summary>
    /// Gets a value indicating whether the last download failed.
    /// </summary>
    public bool IsFailed => _item.State == WeChatVideoDownloadState.Failed;

    /// <summary>
    /// Gets a value indicating whether the video is downloading.
    /// </summary>
    public bool IsDownloading => _item.State == WeChatVideoDownloadState.Downloading;

    /// <summary>
    /// Gets a value indicating whether the primary button uses the accent style.
    /// </summary>
    public bool IsPrimaryAccent =>
        _item.State is WeChatVideoDownloadState.Idle
            or WeChatVideoDownloadState.Failed
            or WeChatVideoDownloadState.Canceled;

    /// <summary>
    /// Gets the text of the state button.
    /// </summary>
    public string PrimaryActionText => _localization[_item.State switch
    {
        WeChatVideoDownloadState.Queued or WeChatVideoDownloadState.Downloading =>
            "Tool.WeChatVideo.Action.Cancel",
        WeChatVideoDownloadState.Completed or WeChatVideoDownloadState.Expired =>
            "Tool.WeChatVideo.Action.Remove",
        WeChatVideoDownloadState.Failed => "Tool.WeChatVideo.Action.Retry",
        _ => "Tool.WeChatVideo.Action.Download",
    }];

    /// <summary>
    /// Gets a value indicating whether the state button can run now.
    /// </summary>
    public bool CanPrimaryAction => _item.State switch
    {
        WeChatVideoDownloadState.Queued or WeChatVideoDownloadState.Downloading => true,
        WeChatVideoDownloadState.Completed or WeChatVideoDownloadState.Expired => true,
        _ => _canDownload,
    };

    /// <summary>
    /// Gets the reason the last download failed, or an empty string.
    /// </summary>
    public string FailureText => _item.State == WeChatVideoDownloadState.Failed
        ? _localization[_item.Failure switch
        {
            WeChatVideoDownloadFailure.DirectoryMissing => "Tool.WeChatVideo.Failure.DirectoryMissing",
            WeChatVideoDownloadFailure.Network => "Tool.WeChatVideo.Failure.Network",
            WeChatVideoDownloadFailure.Protocol => "Tool.WeChatVideo.Failure.Protocol",
            WeChatVideoDownloadFailure.FileWrite => "Tool.WeChatVideo.Failure.FileWrite",
            _ => string.Empty,
        }]
        : string.Empty;

    /// <summary>
    /// Gets a value indicating whether the failure reason is shown.
    /// </summary>
    public bool ShowFailure => _item.State == WeChatVideoDownloadState.Failed &&
        _item.Failure != WeChatVideoDownloadFailure.None;

    /// <summary>
    /// Gets the expired-link hint.
    /// </summary>
    public string ExpiredHint => _localization["Tool.WeChatVideo.Expired.Hint"];

    /// <summary>
    /// Gets a value indicating whether the expired-link hint is shown.
    /// </summary>
    public bool ShowExpiredHint => IsExpired;

    /// <summary>
    /// Gets the gentle hint shown when the downloaded local file no longer exists.
    /// </summary>
    public string LocalFileMissingHint => _localization["Tool.WeChatVideo.FileMissing.Hint"];

    /// <summary>
    /// Gets a value indicating whether the missing-file hint is shown.
    /// </summary>
    public bool ShowLocalFileMissingHint => IsLocalFileMissing;

    /// <summary>
    /// Gets the inline feedback shown when opening or revealing a downloaded file fails.
    /// </summary>
    public string ActionErrorText => _actionErrorKey is null
        ? string.Empty
        : _localization[_actionErrorKey];

    /// <summary>
    /// Gets a value indicating whether inline action error feedback is shown.
    /// </summary>
    public bool ShowActionError => !ShowFailure && !string.IsNullOrEmpty(ActionErrorText);

    /// <summary>
    /// Gets the localized tooltip and accessible name for the open-video button.
    /// </summary>
    public string OpenVideoToolTip => _localization["Tool.WeChatVideo.Action.OpenVideo"];

    /// <summary>
    /// Gets the localized tooltip and accessible name for the show-in-folder button.
    /// </summary>
    public string RevealInFolderToolTip => _localization["Tool.WeChatVideo.Action.RevealInFolder"];

    /// <summary>
    /// Gets the localized tooltip and accessible name for the re-download button.
    /// </summary>
    public string RedownloadToolTip => _localization["Tool.WeChatVideo.Action.Redownload"];

    /// <summary>
    /// Gets the localized tooltip and accessible name for the remove-from-list button.
    /// </summary>
    public string RemoveFromListToolTip => _localization["Tool.WeChatVideo.Action.RemoveFromList"];

    /// <summary>
    /// Gets the localized label for the re-download primary button in the missing-file state.
    /// </summary>
    public string RedownloadActionText => _localization["Tool.WeChatVideo.Action.Redownload"];

    /// <summary>
    /// Gets the number of bytes written so far.
    /// </summary>
    public double ProgressValue => _item.DownloadedBytes;

    /// <summary>
    /// Gets the progress maximum; a placeholder when the total size is unknown.
    /// </summary>
    public double ProgressMaximum => _item.TotalBytes is { } total && total > 0 ? total : 100;

    /// <summary>
    /// Gets a value indicating whether the progress bar has no known total.
    /// </summary>
    public bool IsIndeterminate => IsDownloading && _item.TotalBytes is not > 0;

    /// <summary>
    /// Gets the cover bitmap, greyed out while the link is expired.
    /// </summary>
    public Bitmap? Thumbnail => IsExpired ? _thumbnailGray ?? _thumbnail : _thumbnail;

    /// <summary>
    /// Gets a value indicating whether a cover bitmap is available.
    /// </summary>
    public bool HasThumbnail => Thumbnail is not null;

    /// <summary>
    /// Gets the cover opacity; expired covers are also dimmed.
    /// </summary>
    public double CoverOpacity => IsExpired ? 0.75 : 1.0;

    /// <summary>
    /// Gets or sets a value indicating whether this row is the last in the list.
    /// </summary>
    public bool IsLast
    {
        get => _isLast;
        set => SetProperty(ref _isLast, value);
    }

    /// <summary>
    /// Gets the command that runs the state button's action.
    /// </summary>
    public IRelayCommand PrimaryCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that opens the downloaded video with the system default player.
    /// </summary>
    public IRelayCommand OpenVideoCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that reveals the downloaded video in the system file manager.
    /// </summary>
    public IRelayCommand RevealInFolderCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that re-downloads a completed or missing-file video.
    /// </summary>
    public IRelayCommand RedownloadCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that removes the video from the session list without deleting disk files.
    /// </summary>
    public IRelayCommand RemoveCommand
    {
        get;
    }

    /// <summary>
    /// Gets the selectable quality options, always starting with the default WeChat stream.
    /// </summary>
    public IReadOnlyList<WeChatVideoSpecOption> QualityOptions => _qualityOptions;

    /// <summary>
    /// Gets or sets the currently selected quality option.
    /// </summary>
    public WeChatVideoSpecOption? SelectedQualityOption
    {
        get => _selectedQualityOption;
        set
        {
            if (_syncingQualitySelection || _isDisposed || Equals(_selectedQualityOption, value))
            {
                return;
            }

            if (!CanSelectQuality || value is null)
            {
                OnPropertyChanged();
                return;
            }

            _selectedQualityOption = value;
            OnPropertyChanged();
            _selectQualityAction(this, value.FileFormat);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the quality selector should be shown (when at least two
    /// distinct quality variants exist in addition to the default option).
    /// </summary>
    public bool ShowQualitySelector => _qualityOptions.Count >= 3;

    /// <summary>
    /// Gets a value indicating whether the user may change the quality selection in the current state.
    /// </summary>
    public bool CanSelectQuality => _item.CanSelectQuality;

    /// <summary>
    /// Gets the localized tooltip for the quality selector.
    /// </summary>
    public string QualityToolTip => _localization["Tool.WeChatVideo.Quality.Label"];

    /// <summary>
    /// Takes over a newer session snapshot of the same video.
    /// </summary>
    /// <param name="item">The item after the change.</param>
    public void Update(WeChatVideoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        bool coverChanged = !string.Equals(
            _item.Capture.ThumbUrl,
            item.Capture.ThumbUrl,
            StringComparison.Ordinal);
        _item = item;
        _actionErrorKey = null;
        if (coverChanged)
        {
            ClearThumbnail();
        }

        RefreshAll();
    }

    /// <summary>
    /// Sets or clears an inline error message for a failed row action such as opening or revealing a file.
    /// </summary>
    /// <param name="localizationKey">The localization key of the message, or <see langword="null"/> to clear.</param>
    public void SetActionError(string? localizationKey)
    {
        if (string.Equals(_actionErrorKey, localizationKey, StringComparison.Ordinal))
        {
            return;
        }

        _actionErrorKey = localizationKey;
        OnPropertyChanged(nameof(ActionErrorText));
        OnPropertyChanged(nameof(ShowActionError));
    }

    /// <summary>
    /// Updates whether the download folder currently allows starting or retrying a download.
    /// </summary>
    /// <param name="canDownload">Whether the folder is configured and exists.</param>
    public void SetDownloadAvailable(bool canDownload)
    {
        if (_canDownload == canDownload)
        {
            return;
        }

        _canDownload = canDownload;
        OnPropertyChanged(nameof(CanPrimaryAction));
        OnPropertyChanged(nameof(CanRedownload));
        PrimaryCommand.NotifyCanExecuteChanged();
        RedownloadCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Sets the decoded cover, replacing the previous one.
    /// </summary>
    /// <param name="thumbnail">The cover, or <see langword="null"/> for the placeholder.</param>
    public void SetThumbnail(Bitmap? thumbnail)
    {
        if (ReferenceEquals(_thumbnail, thumbnail))
        {
            return;
        }

        _thumbnailGray?.Dispose();
        _thumbnailGray = null;
        _thumbnail?.Dispose();
        _thumbnail = thumbnail;

        if (thumbnail is not null)
        {
#pragma warning disable CA1031 // A cover that cannot be greyed out must fall back to the colour image instead of failing the list.
            try
            {
                _thumbnailGray = CreateGrayscale(thumbnail);
            }
            catch (Exception)
#pragma warning restore CA1031
            {
                _thumbnailGray = null;
            }
        }

        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(HasThumbnail));
    }

    /// <summary>
    /// Recomputes the localized text after a language change.
    /// </summary>
    public void RefreshText()
    {
        RefreshQualityOptions(forceRebuild: true);
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(HasMeta));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(FailureText));
        OnPropertyChanged(nameof(ShowFailure));
        OnPropertyChanged(nameof(ExpiredHint));
        OnPropertyChanged(nameof(LocalFileMissingHint));
        OnPropertyChanged(nameof(ActionErrorText));
        OnPropertyChanged(nameof(ShowActionError));
        OnPropertyChanged(nameof(OpenVideoToolTip));
        OnPropertyChanged(nameof(RevealInFolderToolTip));
        OnPropertyChanged(nameof(RedownloadToolTip));
        OnPropertyChanged(nameof(RemoveFromListToolTip));
        OnPropertyChanged(nameof(RedownloadActionText));
        OnPropertyChanged(nameof(QualityToolTip));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ClearThumbnail();
    }

    private void RefreshAll()
    {
        RefreshQualityOptions(forceRebuild: false);
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(Key));
        OnPropertyChanged(nameof(ThumbnailUrl));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(HasMeta));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsExpired));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsLocalFileMissing));
        OnPropertyChanged(nameof(ShowCompletedActions));
        OnPropertyChanged(nameof(ShowFileMissingActions));
        OnPropertyChanged(nameof(ShowDefaultPrimaryAction));
        OnPropertyChanged(nameof(CanOpenOrReveal));
        OnPropertyChanged(nameof(CanRedownload));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsPrimaryAccent));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(CanPrimaryAction));
        OnPropertyChanged(nameof(CanSelectQuality));
        OnPropertyChanged(nameof(FailureText));
        OnPropertyChanged(nameof(ShowFailure));
        OnPropertyChanged(nameof(ShowExpiredHint));
        OnPropertyChanged(nameof(ShowLocalFileMissingHint));
        OnPropertyChanged(nameof(ActionErrorText));
        OnPropertyChanged(nameof(ShowActionError));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressMaximum));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(HasThumbnail));
        OnPropertyChanged(nameof(CoverOpacity));
        PrimaryCommand.NotifyCanExecuteChanged();
        OpenVideoCommand.NotifyCanExecuteChanged();
        RevealInFolderCommand.NotifyCanExecuteChanged();
        RedownloadCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    private void RefreshQualityOptions(bool forceRebuild)
    {
        IReadOnlyList<WeChatVideoSpecOption> nextOptions = WeChatVideoSpecSelector.BuildOptions(
            _item.Capture.Specs,
            _localization["Tool.WeChatVideo.Quality.Default"]);

        bool optionsChanged = forceRebuild || !_qualityOptions.SequenceEqual(nextOptions);
        if (optionsChanged)
        {
            _qualityOptions = nextOptions;
            OnPropertyChanged(nameof(QualityOptions));
            OnPropertyChanged(nameof(ShowQualitySelector));
        }

        WeChatVideoSpecOption matched = _qualityOptions.FirstOrDefault(
            option => string.Equals(option.FileFormat, _item.SelectedFileFormat, StringComparison.Ordinal))
            ?? _qualityOptions[0];

        if (optionsChanged || !Equals(_selectedQualityOption, matched))
        {
            _syncingQualitySelection = true;
            try
            {
                _selectedQualityOption = matched;
                OnPropertyChanged(nameof(SelectedQualityOption));
            }
            finally
            {
                _syncingQualitySelection = false;
            }
        }
    }

    private void ClearThumbnail()
    {
        _thumbnailGray?.Dispose();
        _thumbnailGray = null;
        _thumbnail?.Dispose();
        _thumbnail = null;
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(HasThumbnail));
    }

    private void RunPrimaryAction()
    {
        if (_isDisposed || !CanPrimaryAction)
        {
            return;
        }

        _primaryAction(this);
    }

    private void RunOpenVideo()
    {
        if (_isDisposed || !CanOpenOrReveal)
        {
            return;
        }

        _openVideoAction(this);
    }

    private void RunRevealInFolder()
    {
        if (_isDisposed || !CanOpenOrReveal)
        {
            return;
        }

        _revealInFolderAction(this);
    }

    private void RunRedownload()
    {
        if (_isDisposed || !CanRedownload)
        {
            return;
        }

        _redownloadAction(this);
    }

    private void RunRemove()
    {
        if (_isDisposed)
        {
            return;
        }

        _removeAction(this);
    }

    private static WriteableBitmap? CreateGrayscale(Bitmap source)
    {
        PixelSize size = source.PixelSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }

        var target = new WriteableBitmap(
            size,
            source.Dpi,
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);
        try
        {
            using ILockedFramebuffer buffer = target.Lock();
            source.CopyPixels(buffer);
            int stride = buffer.RowBytes;
            long origin = buffer.Address.ToInt64();
            for (int y = 0; y < size.Height; y++)
            {
                long row = origin + ((long)y * stride);
                for (int x = 0; x < size.Width; x++)
                {
                    IntPtr pixel = new(row + (x * 4));
                    byte blue = Marshal.ReadByte(pixel, 0);
                    byte green = Marshal.ReadByte(pixel, 1);
                    byte red = Marshal.ReadByte(pixel, 2);
                    byte gray = (byte)(((red * 299) + (green * 587) + (blue * 114)) / 1000);
                    Marshal.WriteByte(pixel, 0, gray);
                    Marshal.WriteByte(pixel, 1, gray);
                    Marshal.WriteByte(pixel, 2, gray);
                }
            }

            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }
}
