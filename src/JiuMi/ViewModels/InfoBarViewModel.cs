using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application;
using JiuMi.Application.Diagnostics;
using JiuMi.Application.Errors;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;
using JiuMi.Diagnostics;

namespace JiuMi.ViewModels;

/// <summary>
/// Presents one error notice at a time in the shell's global info bar.
/// </summary>
public sealed class InfoBarViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan _warningDisplayDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan _copiedDisplayDuration = TimeSpan.FromSeconds(2);

    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly DiagnosticsExportController _exportController;
    private readonly ILocalizationService _localization;
    private readonly ISettingsStore _settingsStore;
    private readonly ISystemLauncher _systemLauncher;
    private readonly ErrorNoticeBoard _notices = new();
    private readonly DispatcherTimer _dismissTimer;
    private readonly DispatcherTimer _copiedTimer;

    private bool _isActive;
    private bool _isInteractionPaused;
    private bool _isCopied;
    private bool _isDisposed;
    private string? _displayedCode;
    private string? _copiedCode;
    private InfoBarSummary? _summary;
    private string _message = string.Empty;
    private string _secondaryMessage = string.Empty;
    private string _copyText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="InfoBarViewModel"/> class.
    /// </summary>
    /// <param name="errorReporter">Supplies new records and the session detail buffer.</param>
    /// <param name="recordFactory">Creates records for failures raised by the info bar itself.</param>
    /// <param name="localization">Supplies the localized notice text.</param>
    /// <param name="settingsStore">Supplies the current settings for the retry action.</param>
    /// <param name="systemLauncher">Opens the settings folder for the open-folder action.</param>
    /// <param name="exportController">Retries a failed diagnostics export for the retry action.</param>
    public InfoBarViewModel(
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        ILocalizationService localization,
        ISettingsStore settingsStore,
        ISystemLauncher systemLauncher,
        DiagnosticsExportController exportController)
    {
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(systemLauncher);
        ArgumentNullException.ThrowIfNull(exportController);

        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _localization = localization;
        _settingsStore = settingsStore;
        _systemLauncher = systemLauncher;
        _exportController = exportController;

        _dismissTimer = new DispatcherTimer
        {
            Interval = _warningDisplayDuration,
        };
        _dismissTimer.Tick += OnDismissTick;
        _copiedTimer = new DispatcherTimer
        {
            Interval = _copiedDisplayDuration,
        };
        _copiedTimer.Tick += OnCopiedTick;

        ActionCommand = new AsyncRelayCommand(ExecuteActionAsync);
        CloseCommand = new RelayCommand(Dismiss);

        _localization.PropertyChanged += OnLocalizationChanged;
        _errorReporter.Reported += OnErrorReported;

        foreach (ErrorRecord record in _errorReporter.Details.Records)
        {
            _notices.Offer(record);
        }
    }

    /// <summary>
    /// Gets a value indicating whether a notice is visible.
    /// </summary>
    public bool IsOpen => _isActive && (_notices.Current is not null || _summary is not null);

    /// <summary>
    /// Gets a value indicating whether the current notice is an error.
    /// </summary>
    public bool IsError => IsOpen && _notices.Current?.Level == ErrorLevel.Error;

    /// <summary>
    /// Gets a value indicating whether the current notice is a warning.
    /// </summary>
    public bool IsWarning =>
        IsOpen && (_notices.Current?.Level == ErrorLevel.Warning || ActiveSummary is { IsWarning: true });

    /// <summary>
    /// Gets a value indicating whether the current notice reports a completed operation.
    /// </summary>
    public bool IsSuccess => IsOpen && ActiveSummary is { IsWarning: false };

    /// <summary>
    /// Gets a value indicating whether the current notice offers an action button.
    /// </summary>
    public bool HasAction =>
        IsOpen &&
        (_notices.Current?.Action != ErrorNoticeAction.None ||
            ActiveSummary is { } summary && (summary.FolderPath is not null || summary.Action is not null));

    /// <summary>
    /// Gets a value indicating whether the current notice can be closed.
    /// </summary>
    public bool CanClose => IsOpen &&
        (_notices.Current is { IsPersistent: false } || ActiveSummary is not null);

    /// <summary>
    /// Gets a value indicating whether the current notice has error details to copy.
    /// </summary>
    public bool CanCopyDetails => IsOpen && _notices.Current is not null;

    /// <summary>
    /// Gets the localized message of the current notice.
    /// </summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>
    /// Gets the optional second line of the current notice.
    /// </summary>
    public string SecondaryMessage
    {
        get => _secondaryMessage;
        private set => SetProperty(ref _secondaryMessage, value);
    }

    /// <summary>
    /// Gets a value indicating whether the current notice shows a second line.
    /// </summary>
    public bool HasSecondaryMessage => _secondaryMessage.Length > 0;

    /// <summary>
    /// Gets the label of the copy-details action.
    /// </summary>
    public string CopyText
    {
        get => _copyText;
        private set => SetProperty(ref _copyText, value);
    }

    /// <summary>
    /// Gets the accessible name of the current notice.
    /// </summary>
    public string AccessibleName => Message;

    /// <summary>
    /// Gets the accessible name of the close button.
    /// </summary>
    public string CloseAccessibleName => _localization["Common.Close"];

    /// <summary>
    /// Gets the text copied by the details action.
    /// </summary>
    public string DetailsText
    {
        get
        {
            ErrorNotice? notice = _notices.Current;
            ErrorRecord? record = notice is null ? null : _errorReporter.Details.Find(notice.Code);
            return record is null
                ? string.Empty
                : ErrorRecordFormatter.Format(record, _localization["Error." + notice!.Code]);
        }
    }

    private InfoBarSummary? ActiveSummary => _notices.Current is null ? _summary : null;

    /// <summary>
    /// Gets the label of the single primary action.
    /// </summary>
    public string ActionText => _notices.Current?.Action switch
    {
        ErrorNoticeAction.Retry => _localization["Common.Retry"],
        ErrorNoticeAction.OpenFolder => _localization["Common.OpenFolder"],
        _ => ActiveSummary?.ActionText ??
            (ActiveSummary?.FolderPath is not null ? _localization["Common.OpenOutputFolder"] : string.Empty),
    };

    /// <summary>
    /// Gets the command that runs the current notice's primary action.
    /// </summary>
    public IAsyncRelayCommand ActionCommand
    {
        get;
    }

    /// <summary>
    /// Gets the command that closes the current notice.
    /// </summary>
    public IRelayCommand CloseCommand
    {
        get;
    }

    /// <summary>
    /// Starts presenting notices; records that arrived during startup are already queued.
    /// </summary>
    public void Activate()
    {
        if (_isActive)
        {
            return;
        }

        _isActive = true;
        _notices.ShowNext();
        Refresh();
    }

    /// <summary>
    /// Pauses or resumes the warning countdown while the pointer or keyboard focus is inside.
    /// </summary>
    /// <param name="paused">Whether interaction should pause the countdown.</param>
    public void SetInteractionPaused(bool paused)
    {
        if (_isInteractionPaused == paused)
        {
            return;
        }

        _isInteractionPaused = paused;
        if (!_isActive)
        {
            return;
        }

        if (paused)
        {
            _dismissTimer.Stop();
        }
        else if (_notices.Current is { Level: ErrorLevel.Warning, IsPersistent: false } || ActiveSummary is not null)
        {
            RestartDismissTimer();
        }
    }

    /// <summary>
    /// Shows a completed-operation summary in the global bar. It replaces any previous summary and
    /// waits behind error notices.
    /// </summary>
    /// <param name="summary">The summary to show.</param>
    public void ShowSummary(InfoBarSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        _summary = summary;
        _dismissTimer.Stop();
        Refresh();
    }

    /// <summary>
    /// Hides the completed-operation summary, when one is shown.
    /// </summary>
    public void ClearSummary()
    {
        if (_summary is null)
        {
            return;
        }

        _summary = null;
        Refresh();
    }

    /// <summary>
    /// Records a failure to write the details to the clipboard.
    /// </summary>
    /// <param name="error">The clipboard failure.</param>
    public void ReportCopyFailure(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        _errorReporter.Report(_recordFactory.Create(
            ErrorCodes.UnexpectedCopyFailed,
            "Clipboard.Copy",
            error));
    }

    /// <summary>
    /// Shows the copied feedback for the next two seconds.
    /// </summary>
    public void NotifyCopied()
    {
        _isCopied = true;
        _copiedCode = _displayedCode;
        CopyText = _localization["Common.Copied"];
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= OnLocalizationChanged;
        _errorReporter.Reported -= OnErrorReported;

        _dismissTimer.Stop();
        _dismissTimer.Tick -= OnDismissTick;
        _copiedTimer.Stop();
        _copiedTimer.Tick -= OnCopiedTick;
    }

    private void OnErrorReported(object? sender, ErrorRecord record) =>
        Dispatcher.UIThread.Post(() => Accept(record));

    private void Accept(ErrorRecord record)
    {
        if (_isDisposed)
        {
            return;
        }

        _summary = null;
        if (!_notices.Offer(record))
        {
            if (string.Equals(_notices.Current?.Code, record.Code, StringComparison.Ordinal))
            {
                Refresh();
            }

            return;
        }

        if (_isActive && _notices.Current is null)
        {
            _notices.ShowNext();
        }

        Refresh();
    }

    private void Dismiss()
    {
        if (_notices.Current is null)
        {
            if (_summary is null)
            {
                return;
            }

            _summary = null;
            _dismissTimer.Stop();
            Refresh();
            return;
        }

        if (!_notices.DismissCurrent())
        {
            return;
        }

        _dismissTimer.Stop();
        _displayedCode = null;
        _notices.ShowNext();
        Refresh();
    }

    private async Task ExecuteActionAsync()
    {
        InfoBarSummary? summary = ActiveSummary;
        if (summary is not null)
        {
            if (summary.Action is { } action)
            {
                await action();
            }
            else if (summary.FolderPath is { } summaryFolder)
            {
                _systemLauncher.OpenFolder(summaryFolder);
            }

            return;
        }

        ErrorNotice? notice = _notices.Current;
        if (notice is null)
        {
            return;
        }

        switch (notice.Action)
        {
            case ErrorNoticeAction.Retry
                when string.Equals(notice.Code, ErrorCodes.DiagnosticsExportFailed, StringComparison.Ordinal):
                DiagnosticsExportResult result = await _exportController.RetryLastAsync();
                if (result.Succeeded)
                {
                    Dismiss();
                }

                break;
            case ErrorNoticeAction.Retry:
                RetrySettingsSave();
                break;
            case ErrorNoticeAction.OpenFolder:
                if (_settingsStore.Location.DirectoryPath is { } directory)
                {
                    _systemLauncher.OpenFolder(directory);
                }

                break;
        }
    }

    private void RetrySettingsSave()
    {
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current);
        if (result.Error is { } error)
        {
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.SettingsSaveFailed,
                "Settings.Save",
                error));
            return;
        }

        Dismiss();
    }

    private void Refresh()
    {
        string message = string.Empty;
        string secondaryMessage = string.Empty;
        InfoBarSummary? summary = ActiveSummary;
        if (_isActive && _notices.Current is { } notice)
        {
            ErrorRecord? record = _errorReporter.Details.Find(notice.Code);
            message = _localization["Error." + notice.Code];
            int count = record?.Count ?? 1;
            if (count > 1)
            {
                message += _localization.Format("Common.ErrorCountSuffix", count);
            }

            if (notice is { Level: ErrorLevel.Warning, IsPersistent: false } && !_isInteractionPaused)
            {
                if (!string.Equals(_displayedCode, notice.Code, StringComparison.Ordinal))
                {
                    RestartDismissTimer();
                }
            }
            else
            {
                _dismissTimer.Stop();
            }

            _displayedCode = notice.Code;
        }
        else
        {
            if (_isActive && summary is not null)
            {
                message = summary.Message;
                secondaryMessage = summary.SecondaryMessage ?? string.Empty;
                if (!_isInteractionPaused && !_dismissTimer.IsEnabled)
                {
                    RestartDismissTimer();
                }
            }
            else
            {
                _dismissTimer.Stop();
            }

            _displayedCode = null;
        }

        if (_copiedCode is not null &&
            !string.Equals(_displayedCode, _copiedCode, StringComparison.Ordinal))
        {
            _copiedTimer.Stop();
            _isCopied = false;
            _copiedCode = _displayedCode;
        }

        Message = message;
        SecondaryMessage = secondaryMessage;
        CopyText = _isCopied ? _localization["Common.Copied"] : _localization["Common.CopyDetails"];

        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(HasAction));
        OnPropertyChanged(nameof(HasSecondaryMessage));
        OnPropertyChanged(nameof(CanClose));
        OnPropertyChanged(nameof(CanCopyDetails));
        OnPropertyChanged(nameof(ActionText));
        OnPropertyChanged(nameof(AccessibleName));
        OnPropertyChanged(nameof(CloseAccessibleName));
        OnPropertyChanged(nameof(DetailsText));
    }

    private void RestartDismissTimer()
    {
        _dismissTimer.Stop();
        _dismissTimer.Start();
    }

    private void OnDismissTick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();
        if (_notices.Current is { Level: ErrorLevel.Warning, IsPersistent: false })
        {
            Dismiss();
            return;
        }

        if (ActiveSummary is not null)
        {
            ClearSummary();
        }
    }

    private void OnCopiedTick(object? sender, EventArgs e)
    {
        _copiedTimer.Stop();
        _isCopied = false;
        _copiedCode = null;
        CopyText = _localization["Common.CopyDetails"];
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) => Refresh();
}
