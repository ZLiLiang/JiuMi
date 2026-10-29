using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application.Errors;
using JiuMi.Application.ImageConversion;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;

namespace JiuMi.ViewModels;

/// <summary>
/// The image conversion page: the batch parameters, the file queue and the conversion run.
/// </summary>
public sealed class ImageConvertViewModel : ViewModelBase, IDisposable
{
    private const int ThumbnailMaxEdge = 256;
    private const int MaxConcurrency = 4;

    private readonly IImageProbe _probe;
    private readonly IImageConverter _converter;
    private readonly ISettingsStore _settingsStore;
    private readonly ILocalizationService _localization;
    private readonly InfoBarViewModel _infoBar;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly Serilog.ILogger _logger;
    private readonly OutputPathPlanner _pathPlanner;
    private readonly SemaphoreSlim _probeGate;
    private readonly SemaphoreSlim _convertGate;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly IReadOnlyList<string> _targetFormatNames;
    private readonly int _concurrency;
    private int _selectedTargetFormatIndex;
    private double _quality = ImageConversionRules.DefaultQuality;
    private string _maxEdgeText = string.Empty;
    private int? _maxEdge;
    private bool _hasMaxEdgeError;
    private ImageRgbColor _jpegBackgroundColor;
    private string _customColorText;
    private bool _hasCustomColorError;
    private string? _outputDirectory;
    private bool _isAdvancedExpanded;
    private bool _webPLossless;
    private double _pngCompressionLevel = ImageConversionRules.DefaultPngCompressionLevel;
    private bool _keepSourceMetadata;
    private bool _isRunning;
    private bool _isEngineAvailable = true;
    private bool _engineChecked;
    private bool _disposed;
    private int _completedCount;
    private int _totalCount;
    private bool _lastRunCanceled;
    private CancellationTokenSource? _runCancellation;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageConvertViewModel"/> class.
    /// </summary>
    /// <param name="probe">Reads source files once for the list and the conversion.</param>
    /// <param name="converter">Writes the target files.</param>
    /// <param name="settingsStore">Holds the remembered output directory and JPEG background.</param>
    /// <param name="localization">Supplies the interface text.</param>
    /// <param name="infoBar">Shows the end-of-batch summary.</param>
    /// <param name="errorReporter">Reports settings save failures.</param>
    /// <param name="recordFactory">Creates the records reported by this page.</param>
    /// <param name="pathPlanner">Plans output paths without overwriting files.</param>
    /// <param name="logger">Receives probe and conversion diagnostics.</param>
    public ImageConvertViewModel(
        IImageProbe probe,
        IImageConverter converter,
        ISettingsStore settingsStore,
        ILocalizationService localization,
        InfoBarViewModel infoBar,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        OutputPathPlanner pathPlanner,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(converter);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(infoBar);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(pathPlanner);
        ArgumentNullException.ThrowIfNull(logger);

        _probe = probe;
        _converter = converter;
        _settingsStore = settingsStore;
        _localization = localization;
        _infoBar = infoBar;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _logger = logger;
        _pathPlanner = pathPlanner;
        _concurrency = Math.Clamp(Environment.ProcessorCount, 1, MaxConcurrency);
        _probeGate = new SemaphoreSlim(_concurrency, _concurrency);
        _convertGate = new SemaphoreSlim(_concurrency, _concurrency);
        _targetFormatNames = [.. ImageFormatCatalog.OutputFormats.Select(ImageFormatCatalog.GetDisplayName)];

        ImageConvertSettings settings = settingsStore.Current.ImageConvert;
        _outputDirectory = settings.OutputDirectory;
        _jpegBackgroundColor = settings.JpegBackgroundColor;
        _customColorText = _jpegBackgroundColor.ToHex();

        StartCommand = new AsyncRelayCommand(StartAsync, () => CanStart);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        ToggleAdvancedCommand = new RelayCommand(() => IsAdvancedExpanded = !IsAdvancedExpanded);
        UseSourceDirectoryCommand = new RelayCommand(() => SetOutputDirectory(null), () => IsIdle);
        ApplyCustomColorCommand = new RelayCommand(ApplyCustomColor);
        SetBackgroundColorCommand = new RelayCommand<string?>(SetBackgroundColor);
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>
    /// Gets the rows of the conversion queue.
    /// </summary>
    public ObservableCollection<ImageConvertRowViewModel> Rows
    {
        get;
    } = [];

    /// <summary>
    /// Gets the target format names shown by the combo box.
    /// </summary>
    public IReadOnlyList<string> TargetFormatNames => _targetFormatNames;

    /// <summary>
    /// Gets or sets the index of the target format in <see cref="TargetFormatNames"/>.
    /// </summary>
    public int SelectedTargetFormatIndex
    {
        get => _selectedTargetFormatIndex;
        set
        {
            int index = Math.Clamp(value, 0, ImageFormatCatalog.OutputFormats.Count - 1);
            if (!SetProperty(ref _selectedTargetFormatIndex, index))
            {
                return;
            }

            OnPropertyChanged(nameof(TargetFormat));
            OnPropertyChanged(nameof(IsQualityEnabled));
            OnPropertyChanged(nameof(IsJpegTarget));
            OnPropertyChanged(nameof(IsPngTarget));
            OnPropertyChanged(nameof(IsWebPTarget));
            OnPropertyChanged(nameof(CanEditQuality));
            OnPropertyChanged(nameof(CanEditJpegBackground));
            OnPropertyChanged(nameof(CanEditWebPLossy));
            OnPropertyChanged(nameof(CanEditPngCompression));
            foreach (ImageConvertRowViewModel row in Rows)
            {
                row.SetTargetFormat(TargetFormat);
            }
        }
    }

    /// <summary>
    /// Gets the selected target format.
    /// </summary>
    public ImageFormat TargetFormat => ImageFormatCatalog.OutputFormats[_selectedTargetFormatIndex];

    /// <summary>
    /// Gets a value indicating whether the quality slider applies to the target format.
    /// </summary>
    public bool IsQualityEnabled => ImageFormatCatalog.IsLossy(TargetFormat);

    /// <summary>
    /// Gets a value indicating whether the target is JPEG.
    /// </summary>
    public bool IsJpegTarget => TargetFormat == ImageFormat.Jpeg;

    /// <summary>
    /// Gets a value indicating whether the target is PNG.
    /// </summary>
    public bool IsPngTarget => TargetFormat == ImageFormat.Png;

    /// <summary>
    /// Gets a value indicating whether the target is WebP.
    /// </summary>
    public bool IsWebPTarget => TargetFormat == ImageFormat.WebP;

    /// <summary>
    /// Gets a value indicating whether the quality slider may be edited. It does not apply to lossless WebP.
    /// </summary>
    public bool CanEditQuality => IsIdle && IsQualityEnabled && !(IsWebPTarget && _webPLossless);

    /// <summary>
    /// Gets a value indicating whether the JPEG background may be edited.
    /// </summary>
    public bool CanEditJpegBackground => IsIdle && IsJpegTarget;

    /// <summary>
    /// Gets a value indicating whether the WebP lossy switch may be edited.
    /// </summary>
    public bool CanEditWebPLossy => IsIdle && IsWebPTarget;

    /// <summary>
    /// Gets a value indicating whether the PNG compression slider may be edited.
    /// </summary>
    public bool CanEditPngCompression => IsIdle && IsPngTarget;

    /// <summary>
    /// Gets or sets the quality, from 1 to 100.
    /// </summary>
    public double Quality
    {
        get => _quality;
        set
        {
            double clamped = Math.Clamp(value, ImageConversionRules.MinQuality, ImageConversionRules.MaxQuality);
            if (!SetProperty(ref _quality, clamped))
            {
                return;
            }

            OnPropertyChanged(nameof(QualityText));
        }
    }

    /// <summary>
    /// Gets the quality as an integer.
    /// </summary>
    public string QualityText => ((int)Math.Round(_quality)).ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets or sets the longest-edge box. Empty keeps the source size.
    /// </summary>
    public string MaxEdgeText
    {
        get => _maxEdgeText;
        set
        {
            if (!SetProperty(ref _maxEdgeText, value ?? string.Empty))
            {
                return;
            }

            ParseMaxEdge();
        }
    }

    /// <summary>
    /// Gets the parsed longest edge, or <see langword="null"/> for the source size.
    /// </summary>
    public int? MaxEdge => _maxEdge;

    /// <summary>
    /// Gets a value indicating whether the longest-edge box contains an invalid value.
    /// </summary>
    public bool HasMaxEdgeError => _hasMaxEdgeError;

    /// <summary>
    /// Gets the longest-edge error text.
    /// </summary>
    public string MaxEdgeErrorText => _localization["Tool.ImageConvert.MaxEdge.Invalid"];

    /// <summary>
    /// Gets the brush of the JPEG background swatch.
    /// </summary>
    public IBrush JpegBackgroundBrush =>
        new SolidColorBrush(Color.FromRgb(_jpegBackgroundColor.R, _jpegBackgroundColor.G, _jpegBackgroundColor.B));

    /// <summary>
    /// Gets the JPEG background as <c>#RRGGBB</c>.
    /// </summary>
    public string JpegBackgroundHex => _jpegBackgroundColor.ToHex();

    /// <summary>
    /// Gets or sets the text of the custom background colour box.
    /// </summary>
    public string CustomColorText
    {
        get => _customColorText;
        set
        {
            if (!SetProperty(ref _customColorText, value ?? string.Empty))
            {
                return;
            }

            HasCustomColorError = false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the custom background colour is invalid.
    /// </summary>
    public bool HasCustomColorError
    {
        get => _hasCustomColorError;
        private set => SetProperty(ref _hasCustomColorError, value);
    }

    /// <summary>
    /// Gets the custom colour error text.
    /// </summary>
    public string CustomColorErrorText => _localization["Tool.ImageConvert.Background.Invalid"];

    /// <summary>
    /// Gets the output directory label: the source-directory wording or the chosen folder name.
    /// </summary>
    public string OutputDirectoryLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_outputDirectory))
            {
                return _localization["Tool.ImageConvert.Output.Source"];
            }

            string trimmed = _outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(name) ? trimmed : name;
        }
    }

    /// <summary>
    /// Gets the full output directory, or the source-directory wording when no folder is chosen.
    /// </summary>
    public string OutputDirectoryToolTip => string.IsNullOrWhiteSpace(_outputDirectory)
        ? _localization["Tool.ImageConvert.Output.SourceHint"]
        : _outputDirectory;

    /// <summary>
    /// Gets a value indicating whether the advanced section is expanded.
    /// </summary>
    public bool IsAdvancedExpanded
    {
        get => _isAdvancedExpanded;
        private set => SetProperty(ref _isAdvancedExpanded, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether WebP output is lossless.
    /// </summary>
    public bool IsWebPLossless
    {
        get => _webPLossless;
        set
        {
            if (!SetProperty(ref _webPLossless, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsWebPLossy));
            OnPropertyChanged(nameof(CanEditQuality));
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether WebP output is lossy; the checkbox shown to the user.
    /// </summary>
    public bool IsWebPLossy
    {
        get => !_webPLossless;
        set => IsWebPLossless = !value;
    }

    /// <summary>
    /// Gets or sets the PNG compression level, from 0 to 9.
    /// </summary>
    public double PngCompressionLevel
    {
        get => _pngCompressionLevel;
        set
        {
            double clamped = Math.Clamp(
                value,
                ImageConversionRules.MinPngCompressionLevel,
                ImageConversionRules.MaxPngCompressionLevel);
            if (!SetProperty(ref _pngCompressionLevel, clamped))
            {
                return;
            }

            OnPropertyChanged(nameof(PngCompressionText));
        }
    }

    /// <summary>
    /// Gets the PNG compression level as an integer.
    /// </summary>
    public string PngCompressionText => ((int)Math.Round(_pngCompressionLevel)).ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// Gets or sets a value indicating whether source metadata is written to the output.
    /// </summary>
    public bool KeepSourceMetadata
    {
        get => _keepSourceMetadata;
        set => SetProperty(ref _keepSourceMetadata, value);
    }

    /// <summary>
    /// Gets a value indicating whether the queue has rows.
    /// </summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the empty state replaces the parameters and the list.
    /// </summary>
    public bool ShowEmptyState => !HasRows;

    /// <summary>
    /// Gets a value indicating whether a batch is running.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (!SetProperty(ref _isRunning, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(CanAddFiles));
            OnPropertyChanged(nameof(CanEditQuality));
            OnPropertyChanged(nameof(CanEditJpegBackground));
            OnPropertyChanged(nameof(CanEditWebPLossy));
            OnPropertyChanged(nameof(CanEditPngCompression));
            CancelCommand.NotifyCanExecuteChanged();
            UseSourceDirectoryCommand.NotifyCanExecuteChanged();
            RefreshStartState();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the parameters and the queue may be edited.
    /// </summary>
    public bool IsIdle => !_isRunning;

    /// <summary>
    /// Gets a value indicating whether files may be added.
    /// </summary>
    public bool CanAddFiles => !_isRunning;

    /// <summary>
    /// Gets a value indicating whether the imaging engine loaded.
    /// </summary>
    public bool IsEngineAvailable
    {
        get => _isEngineAvailable;
        private set
        {
            if (!SetProperty(ref _isEngineAvailable, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsEngineUnavailable));
            RefreshStartState();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the page must explain the engine degradation.
    /// </summary>
    public bool IsEngineUnavailable => !_isEngineAvailable;

    /// <summary>
    /// Gets the engine degradation text.
    /// </summary>
    public string EngineUnavailableText => _localization["Tool.ImageConvert.EngineUnavailable"];

    /// <summary>
    /// Gets the number of finished files in the current or last batch.
    /// </summary>
    public double ProgressValue => _completedCount;

    /// <summary>
    /// Gets the number of files in the current or last batch, at least one.
    /// </summary>
    public double ProgressMaximum => Math.Max(1, _totalCount);

    /// <summary>
    /// Gets the progress label.
    /// </summary>
    public string ProgressText => _localization.Format(
        "Tool.ImageConvert.Progress",
        _completedCount,
        _totalCount);

    /// <summary>
    /// Gets a value indicating whether the batch may start.
    /// </summary>
    public bool CanStart => !_isRunning &&
        _isEngineAvailable &&
        !_hasMaxEdgeError &&
        Rows.Any(row => row.IsConvertible);

    /// <summary>
    /// Gets the command that starts the batch.
    /// </summary>
    public IAsyncRelayCommand StartCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that cancels the running batch.
    /// </summary>
    public IRelayCommand CancelCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that expands or collapses the advanced section.
    /// </summary>
    public IRelayCommand ToggleAdvancedCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that writes beside each source file.
    /// </summary>
    public IRelayCommand UseSourceDirectoryCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that applies the typed custom background colour.
    /// </summary>
    public IRelayCommand ApplyCustomColorCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that picks one of the background preset colours.
    /// </summary>
    public IRelayCommand<string?> SetBackgroundColorCommand
    {
        get;
    }

    /// <summary>
    /// Checks the imaging engine when the page first opens.
    /// </summary>
    public void Activate()
    {
        if (_engineChecked || _disposed)
        {
            return;
        }

        _engineChecked = true;
        _ = CheckEngineAsync();
    }

    /// <summary>
    /// Records a failure to show a file or folder picker without breaking the page.
    /// </summary>
    /// <param name="exception">The picker failure.</param>
    public void ReportPickerFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _logger.Warning(exception, "A file or folder picker failed");
    }

    /// <summary>
    /// Adds files to the queue, skipping duplicates and missing files. Unsupported extensions are
    /// queued as failed rows so the list explains why they cannot be converted.
    /// </summary>
    /// <param name="paths">The file paths to add.</param>
    /// <returns>A task that completes when the files are queued; probing continues in the background.</returns>
    public Task AddFilesAsync(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (_disposed || _isRunning)
        {
            return Task.CompletedTask;
        }

        var added = new List<ImageConvertRowViewModel>();
        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(path);
            if (Rows.Any(row => string.Equals(row.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var row = new ImageConvertRowViewModel(fullPath, TargetFormat, _localization, RemoveRow);
            Rows.Add(row);
            added.Add(row);
        }

        if (added.Count == 0)
        {
            return Task.CompletedTask;
        }

        OnRowsChanged();
        foreach (ImageConvertRowViewModel row in added)
        {
            _ = ProbeRowAsync(row);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Changes the output directory and remembers it; <see langword="null"/> writes beside each source.
    /// </summary>
    /// <param name="path">The chosen folder or <see langword="null"/>.</param>
    public void SetOutputDirectory(string? path)
    {
        string? normalized = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        if (string.Equals(_outputDirectory, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _outputDirectory = normalized;
        OnPropertyChanged(nameof(OutputDirectoryLabel));
        OnPropertyChanged(nameof(OutputDirectoryToolTip));
        PersistSettings();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        _runCancellation = null;
        foreach (ImageConvertRowViewModel row in Rows)
        {
            row.Dispose();
        }

        Rows.Clear();
        _lifetime.Dispose();
        _probeGate.Dispose();
        _convertGate.Dispose();
        _localization.PropertyChanged -= OnLocalizationChanged;
    }

    private async Task CheckEngineAsync()
    {
        bool available;
        try
        {
            available = await Task.Run(() => _probe.IsAvailable && _converter.IsAvailable, _lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Checking the imaging engine failed");
            IsEngineAvailable = false;
            return;
        }

        IsEngineAvailable = available;
        if (!available)
        {
            _logger.Warning("The imaging engine did not load; image conversion is disabled");
        }
    }

    private async Task ProbeRowAsync(ImageConvertRowViewModel row)
    {
        CancellationToken token = _lifetime.Token;
        try
        {
            await _probeGate.WaitAsync(token);
            try
            {
                ImageProbeResult probe = await _probe.ProbeAsync(row.SourcePath, ThumbnailMaxEdge, token);
                if (_disposed || row.IsDisposed)
                {
                    probe.Dispose();
                    return;
                }

                row.ApplyProbe(probe);
                if (probe.Error is { } error)
                {
                    _logger.Debug(error, "Probing {SourcePath} failed", row.SourcePath);
                }
            }
            finally
            {
                _probeGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Probing {SourcePath} failed", row.SourcePath);
            if (!row.IsDisposed)
            {
                row.MarkFailed(ImageConversionFailureReason.DecodeFailed);
            }
        }
        finally
        {
            RefreshStartState();
        }
    }

    private async Task StartAsync()
    {
        if (_isRunning || !TryBuildRequest(out ImageConversionRequest request))
        {
            return;
        }

        ImageConvertRowViewModel[] batchRows =
            [.. Rows.Where(row => row.Status is ImageConvertRowStatus.Pending or ImageConvertRowStatus.Failed)];
        ImageConvertRowViewModel[] candidates = [.. batchRows.Where(row => row.IsConvertible)];
        var planned = new Dictionary<ImageConvertRowViewModel, string>();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ImageConvertRowViewModel row in candidates)
        {
            try
            {
                planned[row] = _pathPlanner.Plan(
                    row.SourcePath,
                    request.OutputDirectory,
                    request.TargetFormat,
                    reserved);
                reserved.Add(planned[row]);
            }
            catch (ArgumentException exception)
            {
                row.MarkFailed(ImageConversionFailureReason.WriteFailed);
                _logger.Warning(exception, "Planning the output of {SourcePath} failed", row.SourcePath);
            }
        }

        ImageConvertRowViewModel[] toRun = [.. candidates.Where(planned.ContainsKey)];
        if (toRun.Length == 0)
        {
            RefreshStartState();
            return;
        }

        _runCancellation = new CancellationTokenSource();
        CancellationToken token = _runCancellation.Token;
        _lastRunCanceled = false;
        _completedCount = 0;
        _totalCount = batchRows.Length;
        RefreshProgress();
        IsRunning = true;
        try
        {
            await Task.WhenAll(toRun.Select(row => ConvertRowAsync(row, request, planned[row], token)));
        }
        catch (OperationCanceledException)
        {
            _lastRunCanceled = true;
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "The image conversion batch failed");
        }
        finally
        {
            IsRunning = false;
            _runCancellation?.Dispose();
            _runCancellation = null;
            _completedCount = batchRows.Count(row => row.IsDone || row.IsFailed);
            RefreshProgress();
            ShowSummary(batchRows, request);
        }
    }

    private async Task ConvertRowAsync(
        ImageConvertRowViewModel row,
        ImageConversionRequest request,
        string outputPath,
        CancellationToken token)
    {
        bool acquired = false;
        try
        {
            await _convertGate.WaitAsync(token);
            acquired = true;
            token.ThrowIfCancellationRequested();
            row.MarkConverting();
            ImageConversionResult result = await _converter.ConvertAsync(row.Probe!, request, outputPath, token);
            if (result.Succeeded)
            {
                row.MarkDone(result.OutputPath!);
            }
            else
            {
                row.MarkFailed(result.Failure!.Value);
                if (result.Error is { } error)
                {
                    _logger.Warning(error, "Converting {SourcePath} failed", row.SourcePath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            row.MarkPending();
            throw;
        }
        catch (Exception exception)
        {
            row.MarkFailed(ImageConversionFailureReason.EncodeFailed);
            _logger.Warning(exception, "Converting {SourcePath} failed", row.SourcePath);
        }
        finally
        {
            if (acquired)
            {
                _convertGate.Release();
                if (row.IsDone || row.IsFailed)
                {
                    _completedCount++;
                    RefreshProgress();
                }
            }

            if (row.IsDone)
            {
                row.ReleaseDecoded();
            }

            RefreshStartState();
        }
    }

    private bool TryBuildRequest(out ImageConversionRequest request)
    {
        request = new ImageConversionRequest
        {
            TargetFormat = TargetFormat,
            Quality = (int)Math.Round(_quality),
            MaxEdge = _maxEdge,
            JpegBackgroundColor = _jpegBackgroundColor,
            OutputDirectory = _outputDirectory,
            Advanced = new ImageConversionAdvancedOptions
            {
                WebPLossless = _webPLossless,
                PngCompressionLevel = (int)Math.Round(_pngCompressionLevel),
                KeepSourceMetadata = _keepSourceMetadata,
            },
        };

        IReadOnlyList<ImageConversionValidationError> errors = ImageConversionRules.Validate(request);
        if (errors.Count > 0)
        {
            _logger.Warning("The conversion request was rejected: {Errors}", errors);
            return false;
        }

        return true;
    }

    private void ShowSummary(IReadOnlyList<ImageConvertRowViewModel> rows, ImageConversionRequest request)
    {
        int done = rows.Count(row => row.IsDone);
        int failed = rows.Count(row => row.IsFailed);
        string message;
        bool isWarning;
        if (_lastRunCanceled)
        {
            message = _localization.Format("Tool.ImageConvert.Summary.Cancelled", done, failed);
            isWarning = true;
        }
        else if (failed == 0)
        {
            message = _localization.Format("Tool.ImageConvert.Summary.Success", done);
            isWarning = false;
        }
        else
        {
            message = _localization.Format("Tool.ImageConvert.Summary.Failure", done, failed);
            isWarning = true;
        }

        _infoBar.ShowSummary(new InfoBarSummary(message, isWarning, FindOutputFolder(rows, request.OutputDirectory)));
    }

    private static string? FindOutputFolder(
        IReadOnlyList<ImageConvertRowViewModel> rows,
        string? outputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            return outputDirectory;
        }

        foreach (ImageConvertRowViewModel row in rows)
        {
            if (row.OutputPath is { } output)
            {
                return Path.GetDirectoryName(output);
            }
        }

        return rows.Count > 0 ? Path.GetDirectoryName(rows[0].SourcePath) : null;
    }

    private void Cancel()
    {
        _runCancellation?.Cancel();
    }

    private void RemoveRow(ImageConvertRowViewModel row)
    {
        if (!Rows.Remove(row))
        {
            return;
        }

        row.Dispose();
        OnRowsChanged();
    }

    private void OnRowsChanged()
    {
        if (!_isRunning)
        {
            _completedCount = Rows.Count(row => row.IsDone);
            _totalCount = Rows.Count;
            RefreshProgress();
        }

        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ShowEmptyState));
        RefreshStartState();
    }

    private void RefreshStartState()
    {
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
    }

    private void RefreshProgress()
    {
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(ProgressMaximum));
        OnPropertyChanged(nameof(ProgressText));
    }

    private void ParseMaxEdge()
    {
        int? parsed = null;
        bool invalid = false;
        string text = _maxEdgeText.Trim();
        if (text.Length > 0)
        {
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0)
            {
                parsed = value;
            }
            else
            {
                invalid = true;
            }
        }

        _maxEdge = parsed;
        if (_hasMaxEdgeError != invalid)
        {
            _hasMaxEdgeError = invalid;
            OnPropertyChanged(nameof(HasMaxEdgeError));
        }

        OnPropertyChanged(nameof(MaxEdgeErrorText));
        OnPropertyChanged(nameof(MaxEdge));
        RefreshStartState();
    }

    private void SetBackgroundColor(string? hex)
    {
        if (ImageRgbColor.TryParse(hex, out ImageRgbColor color))
        {
            SetJpegBackgroundColor(color);
        }
    }

    private void ApplyCustomColor()
    {
        if (!ImageRgbColor.TryParse(_customColorText, out ImageRgbColor color))
        {
            HasCustomColorError = true;
            return;
        }

        SetJpegBackgroundColor(color);
    }

    private void SetJpegBackgroundColor(ImageRgbColor color)
    {
        HasCustomColorError = false;
        _customColorText = color.ToHex();
        OnPropertyChanged(nameof(CustomColorText));
        if (_jpegBackgroundColor == color)
        {
            return;
        }

        _jpegBackgroundColor = color;
        OnPropertyChanged(nameof(JpegBackgroundBrush));
        OnPropertyChanged(nameof(JpegBackgroundHex));
        PersistSettings();
    }

    private void PersistSettings()
    {
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current with
        {
            ImageConvert = new ImageConvertSettings
            {
                OutputDirectory = _outputDirectory,
                JpegBackgroundColor = _jpegBackgroundColor,
            },
        });

        if (result.Error is { } error)
        {
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.SettingsSaveFailed,
                "Settings.Save",
                error));
        }
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(
            e.PropertyName,
            nameof(ILocalizationService.CurrentLanguage),
            StringComparison.Ordinal))
        {
            return;
        }

        foreach (ImageConvertRowViewModel row in Rows)
        {
            row.RefreshText();
        }

        OnPropertyChanged(nameof(MaxEdgeErrorText));
        OnPropertyChanged(nameof(CustomColorErrorText));
        OnPropertyChanged(nameof(OutputDirectoryLabel));
        OnPropertyChanged(nameof(OutputDirectoryToolTip));
        OnPropertyChanged(nameof(EngineUnavailableText));
        OnPropertyChanged(nameof(ProgressText));
    }
}
