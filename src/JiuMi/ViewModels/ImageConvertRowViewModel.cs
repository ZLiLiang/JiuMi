using System;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application.ImageConversion;
using JiuMi.Application.Localization;

namespace JiuMi.ViewModels;

/// <summary>
/// One row of the conversion list: the probe result, the thumbnail, the target format and the status.
/// </summary>
public sealed class ImageConvertRowViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly Action<ImageConvertRowViewModel> _remove;
    private ImageProbeResult? _probe;
    private Bitmap? _thumbnail;
    private ImageFormat _targetFormat;
    private ImageConvertRowStatus _status = ImageConvertRowStatus.Pending;
    private ImageConversionFailureReason? _failure;
    private string? _outputPath;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageConvertRowViewModel"/> class.
    /// </summary>
    /// <param name="sourcePath">The absolute source file path.</param>
    /// <param name="targetFormat">The format the page currently writes.</param>
    /// <param name="localization">Supplies the row text.</param>
    /// <param name="remove">Removes the row from the queue.</param>
    public ImageConvertRowViewModel(
        string sourcePath,
        ImageFormat targetFormat,
        ILocalizationService localization,
        Action<ImageConvertRowViewModel> remove)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(remove);

        SourcePath = sourcePath;
        _targetFormat = targetFormat;
        _localization = localization;
        _remove = remove;
        RemoveCommand = new RelayCommand(Remove, () => CanRemove);
    }

    /// <summary>
    /// Gets the absolute source file path.
    /// </summary>
    public string SourcePath
    {
        get;
    }

    /// <summary>
    /// Gets the source file name shown on the main line.
    /// </summary>
    public string FileName => Path.GetFileName(SourcePath);

    /// <summary>
    /// Gets the source format when the probe succeeded.
    /// </summary>
    public ImageFormat? SourceFormat => _probe?.Format;

    /// <summary>
    /// Gets the composite width in pixels, or 0 before the probe finished.
    /// </summary>
    public int PixelWidth => _probe?.PixelWidth ?? 0;

    /// <summary>
    /// Gets the composite height in pixels, or 0 before the probe finished.
    /// </summary>
    public int PixelHeight => _probe?.PixelHeight ?? 0;

    /// <summary>
    /// Gets the source format label shown while the thumbnail is missing.
    /// </summary>
    public string ThumbnailLabel =>
        SourceFormat is { } format
            ? ImageFormatCatalog.GetDisplayName(format)
            : _localization["Tool.ImageConvert.Row.UnknownFormat"];

    /// <summary>
    /// Gets the decoded thumbnail, or <see langword="null"/> before the probe finished.
    /// </summary>
    public Bitmap? Thumbnail => _thumbnail;

    /// <summary>
    /// Gets a value indicating whether a thumbnail is available.
    /// </summary>
    public bool HasThumbnail => _thumbnail is not null;

    /// <summary>
    /// Gets the target format written for this row.
    /// </summary>
    public ImageFormat TargetFormat => _targetFormat;

    /// <summary>
    /// Gets the row status.
    /// </summary>
    public ImageConvertRowStatus Status => _status;

    /// <summary>
    /// Gets a value indicating whether the row is queued.
    /// </summary>
    public bool IsPending => _status == ImageConvertRowStatus.Pending;

    /// <summary>
    /// Gets a value indicating whether the row is being converted.
    /// </summary>
    public bool IsConverting => _status == ImageConvertRowStatus.Converting;

    /// <summary>
    /// Gets a value indicating whether the output was written.
    /// </summary>
    public bool IsDone => _status == ImageConvertRowStatus.Done;

    /// <summary>
    /// Gets a value indicating whether the row failed.
    /// </summary>
    public bool IsFailed => _status == ImageConvertRowStatus.Failed;

    /// <summary>
    /// Gets a value indicating whether the row may be removed from the queue.
    /// </summary>
    public bool CanRemove => !IsConverting;

    /// <summary>
    /// Gets the probe that decoded this file, or <see langword="null"/> before it finished.
    /// </summary>
    public ImageProbeResult? Probe => _probe;

    /// <summary>
    /// Gets a value indicating whether the row can be converted in the next batch.
    /// </summary>
    public bool IsConvertible => _probe is { IsDecodable: true } && _status is ImageConvertRowStatus.Pending or ImageConvertRowStatus.Failed;

    /// <summary>
    /// Gets the written file path, or <see langword="null"/> before the row succeeded.
    /// </summary>
    public string? OutputPath => _outputPath;

    /// <summary>
    /// Gets the localized status chip text.
    /// </summary>
    public string StatusText => _localization[_status switch
    {
        ImageConvertRowStatus.Converting => "Tool.ImageConvert.Status.Converting",
        ImageConvertRowStatus.Done => "Tool.ImageConvert.Status.Done",
        ImageConvertRowStatus.Failed => "Tool.ImageConvert.Status.Failed",
        _ => "Tool.ImageConvert.Status.Pending",
    }];

    /// <summary>
    /// Gets the second line: size, source → target and the recompression hint.
    /// </summary>
    public string MetaText
    {
        get
        {
            string target = ImageFormatCatalog.GetDisplayName(_targetFormat);
            if (_probe is { Format: { } source } probe && probe.PixelWidth > 0)
            {
                string meta = _localization.Format(
                    "Tool.ImageConvert.Row.Meta",
                    probe.PixelWidth,
                    probe.PixelHeight,
                    ImageFormatCatalog.GetDisplayName(source),
                    target);
                return ShowRecompressHint
                    ? meta + _localization["Tool.ImageConvert.Row.Recompress"]
                    : meta;
            }

            return _localization.Format("Tool.ImageConvert.Row.MetaPlain", ThumbnailLabel, target);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the row converts to its own format and will be recompressed.
    /// </summary>
    public bool ShowRecompressHint => SourceFormat is { } source && source == _targetFormat;

    /// <summary>
    /// Gets the localized failure reason, or an empty string while the row has not failed.
    /// </summary>
    public string FailureText => _failure is { } reason
        ? reason switch
        {
            ImageConversionFailureReason.UnsupportedFormat => _localization["Tool.ImageConvert.Failure.Unsupported"],
            ImageConversionFailureReason.DecodeFailed when SourceFormat is ImageFormat.Psd or ImageFormat.Psb =>
                _localization["Tool.ImageConvert.Failure.DecodePsd"],
            ImageConversionFailureReason.DecodeFailed => _localization["Tool.ImageConvert.Failure.Decode"],
            ImageConversionFailureReason.EncodeFailed => _localization["Tool.ImageConvert.Failure.Encode"],
            _ => _localization["Tool.ImageConvert.Failure.Write"],
        }
        : string.Empty;

    /// <summary>
    /// Gets a value indicating whether a failure reason is shown.
    /// </summary>
    public bool HasFailure => _failure is not null;

    /// <summary>
    /// Gets the command that removes the row from the queue.
    /// </summary>
    public IRelayCommand RemoveCommand
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether the row was removed and disposed.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Takes a successful or failed probe result over.
    /// </summary>
    /// <param name="probe">The probe result; the row owns it from now on.</param>
    public void ApplyProbe(ImageProbeResult probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        _probe?.Dispose();
        _probe = probe;
        if (probe.ThumbnailPng is { Length: > 0 } bytes)
        {
            Bitmap? previous = _thumbnail;
            _thumbnail = new Bitmap(new MemoryStream(bytes));
            previous?.Dispose();
        }

        _failure = probe.Failure;
        _status = probe.IsDecodable ? ImageConvertRowStatus.Pending : ImageConvertRowStatus.Failed;
        RefreshText();
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(HasThumbnail));
        RaiseStatusChanged();
    }

    /// <summary>
    /// Changes the target format shown by the row.
    /// </summary>
    /// <param name="format">The format the page now writes.</param>
    public void SetTargetFormat(ImageFormat format)
    {
        if (_targetFormat == format)
        {
            return;
        }

        _targetFormat = format;
        OnPropertyChanged(nameof(TargetFormat));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(ShowRecompressHint));
    }

    /// <summary>
    /// Marks the row as being converted.
    /// </summary>
    public void MarkConverting()
    {
        _status = ImageConvertRowStatus.Converting;
        _failure = null;
        RaiseStatusChanged();
        RefreshText();
    }

    /// <summary>
    /// Marks the row as written.
    /// </summary>
    /// <param name="outputPath">The written file path.</param>
    public void MarkDone(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        _outputPath = outputPath;
        _status = ImageConvertRowStatus.Done;
        _failure = null;
        RaiseStatusChanged();
        RefreshText();
    }

    /// <summary>
    /// Marks the row as failed.
    /// </summary>
    /// <param name="reason">Why the row failed.</param>
    public void MarkFailed(ImageConversionFailureReason reason)
    {
        _failure = reason;
        _status = ImageConvertRowStatus.Failed;
        RaiseStatusChanged();
        RefreshText();
    }

    /// <summary>
    /// Returns the row to the queue after a cancelled run.
    /// </summary>
    public void MarkPending()
    {
        _status = ImageConvertRowStatus.Pending;
        RaiseStatusChanged();
        RefreshText();
    }

    /// <summary>
    /// Releases the decoded image once the row no longer needs to be converted.
    /// </summary>
    public void ReleaseDecoded() => _probe?.Dispose();

    /// <summary>
    /// Recomputes every localized text after a language change.
    /// </summary>
    public void RefreshText()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(FailureText));
        OnPropertyChanged(nameof(HasFailure));
        OnPropertyChanged(nameof(ThumbnailLabel));
        OnPropertyChanged(nameof(ShowRecompressHint));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _probe?.Dispose();
        _probe = null;
        _thumbnail?.Dispose();
        _thumbnail = null;
    }

    private void Remove()
    {
        if (!CanRemove)
        {
            return;
        }

        _remove(this);
    }

    private void RaiseStatusChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsConverting));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(IsConvertible));
        RemoveCommand.NotifyCanExecuteChanged();
    }
}
