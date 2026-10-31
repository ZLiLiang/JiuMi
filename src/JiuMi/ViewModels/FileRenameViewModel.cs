using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using JiuMi.Application.Errors;
using JiuMi.Application.FileRename;
using JiuMi.Application.Localization;
using JiuMi.Application.Settings;

namespace JiuMi.ViewModels;

/// <summary>
/// The batch rename page: the parameters, the live preview, the confirmed run, the last-batch undo
/// and the JSON log export.
/// </summary>
public sealed class FileRenameViewModel : ViewModelBase, IDisposable
{
    private readonly IRenameScanner _scanner;
    private readonly IRenameExecutor _executor;
    private readonly IRenameBatchLogWriter _logWriter;
    private readonly IRenamePathProbe _pathProbe;
    private readonly ISettingsStore _settingsStore;
    private readonly ILocalizationService _localization;
    private readonly InfoBarViewModel _infoBar;
    private readonly IErrorReporter _errorReporter;
    private readonly ErrorRecordFactory _recordFactory;
    private readonly TimeProvider _timeProvider;
    private readonly Serilog.ILogger _logger;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _scanTimer;
    private readonly CancellationTokenSource _lifetime = new();
    private string? _selectedDirectory;
    private RenameScanResult? _scan;
    private RenamePlan _plan = new([], [], 0);
    private RenameBatch? _lastBatch;
    private RenameTarget _target;
    private RenameRuleLevel _level;
    private RenameBasicTask _basicTask;
    private string _basicFind;
    private string _basicReplace;
    private RenameAffixPosition _affixPosition;
    private string _affixText;
    private string _sequenceName;
    private int _sequencePadWidth;
    private RenameRuleKind _advancedKind;
    private string _advancedFind;
    private string _advancedReplace;
    private bool _advancedUseRegex;
    private string _template;
    private bool _includeSubdirectories;
    private string _extensions;
    private bool _backupEnabled;
    private bool _advancedEdited;
    private bool _isSkipPanelOpen;
    private bool _isBusy;
    private bool _batchVisible;
    private bool _scanStale;
    private bool _isScanning;
    private int _scanVersion;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenameViewModel"/> class.
    /// </summary>
    /// <param name="scanner">Reads the selected directory.</param>
    /// <param name="executor">Renames and restores items.</param>
    /// <param name="logWriter">Writes the last batch as JSON.</param>
    /// <param name="pathProbe">Answers whether a target path is already taken.</param>
    /// <param name="settingsStore">Holds the persisted rule parameters.</param>
    /// <param name="localization">Supplies the interface text.</param>
    /// <param name="infoBar">Shows the end-of-batch summary.</param>
    /// <param name="errorReporter">Reports per-item and settings failures.</param>
    /// <param name="recordFactory">Creates the records reported by this page.</param>
    /// <param name="timeProvider">Supplies today's date and the batch timestamps.</param>
    /// <param name="logger">Receives scan, execution and export diagnostics.</param>
    public FileRenameViewModel(
        IRenameScanner scanner,
        IRenameExecutor executor,
        IRenameBatchLogWriter logWriter,
        IRenamePathProbe pathProbe,
        ISettingsStore settingsStore,
        ILocalizationService localization,
        InfoBarViewModel infoBar,
        IErrorReporter errorReporter,
        ErrorRecordFactory recordFactory,
        TimeProvider timeProvider,
        Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(logWriter);
        ArgumentNullException.ThrowIfNull(pathProbe);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(infoBar);
        ArgumentNullException.ThrowIfNull(errorReporter);
        ArgumentNullException.ThrowIfNull(recordFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _scanner = scanner;
        _executor = executor;
        _logWriter = logWriter;
        _pathProbe = pathProbe;
        _settingsStore = settingsStore;
        _localization = localization;
        _infoBar = infoBar;
        _errorReporter = errorReporter;
        _recordFactory = recordFactory;
        _timeProvider = timeProvider;
        _logger = logger;

        FileRenameSettings settings = settingsStore.Current.FileRename;
        _level = settings.RuleLevel;
        _basicTask = settings.BasicTask;
        _basicFind = settings.BasicFind;
        _basicReplace = settings.BasicReplace;
        _affixPosition = settings.AffixPosition;
        _affixText = settings.AffixText;
        _sequenceName = settings.SequenceName;
        _sequencePadWidth = settings.SequencePadWidth;
        _advancedKind = settings.AdvancedKind;
        _advancedFind = settings.AdvancedFind;
        _advancedReplace = settings.AdvancedReplace;
        _advancedUseRegex = settings.AdvancedUseRegex;
        _template = settings.Template;
        _advancedEdited = settings.AdvancedEdited;
        _includeSubdirectories = settings.IncludeSubdirectories;
        _extensions = settings.Extensions;
        _backupEnabled = settings.BackupEnabled;

        SetTargetCommand = new RelayCommand<RenameTarget>(SetTarget);
        SetLevelCommand = new RelayCommand<RenameRuleLevel>(SetLevel);
        SetAffixPositionCommand = new RelayCommand<RenameAffixPosition>(SetAffixPosition);
        ToggleSkipCommand = new RelayCommand(ToggleSkip);
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => CanUndo);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += OnSaveTick;
        _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _scanTimer.Tick += OnScanTick;
        _localization.PropertyChanged += OnLocalizationChanged;
    }

    /// <summary>Gets the preview rows in execution order.</summary>
    public ObservableCollection<FileRenameRowViewModel> Rows
    {
        get;
    } = [];

    /// <summary>Gets the hidden, system and linked items left out of the batch.</summary>
    public ObservableCollection<FileRenameSkipRowViewModel> SkippedRows
    {
        get;
    } = [];

    /// <summary>Gets a value indicating whether a folder is selected.</summary>
    public bool HasDirectory => _selectedDirectory is not null;

    /// <summary>Gets a value indicating whether the empty state replaces the card.</summary>
    public bool ShowEmptyState => !HasDirectory;

    /// <summary>Gets a value indicating whether files are renamed.</summary>
    public bool IsFilesMode => _target == RenameTarget.Files;

    /// <summary>Gets a value indicating whether folders are renamed.</summary>
    public bool IsFoldersMode => _target == RenameTarget.Folders;

    /// <summary>Gets a value indicating whether the basic level is shown.</summary>
    public bool IsBasicLevel => _level == RenameRuleLevel.Basic;

    /// <summary>Gets a value indicating whether the advanced level is shown.</summary>
    public bool IsAdvancedLevel => _level == RenameRuleLevel.Advanced;

    /// <summary>Gets a value indicating whether the basic replace fields are shown.</summary>
    public bool ShowBasicReplaceFields => IsBasicLevel && _basicTask == RenameBasicTask.Replace;

    /// <summary>Gets a value indicating whether the basic affix fields are shown.</summary>
    public bool ShowBasicAffixFields => IsBasicLevel && _basicTask == RenameBasicTask.Affix;

    /// <summary>Gets a value indicating whether the basic sequence fields are shown.</summary>
    public bool ShowBasicSequenceFields => IsBasicLevel && _basicTask == RenameBasicTask.Sequence;

    /// <summary>Gets a value indicating whether the advanced replace fields are shown.</summary>
    public bool ShowAdvancedReplaceFields => IsAdvancedLevel && _advancedKind == RenameRuleKind.Replace;

    /// <summary>Gets a value indicating whether the advanced template field is shown.</summary>
    public bool ShowAdvancedTemplateFields => IsAdvancedLevel && _advancedKind == RenameRuleKind.Template;

    /// <summary>Gets a value indicating whether the extension filter applies and is shown.</summary>
    public bool ShowExtensionFilter => IsFilesMode;

    /// <summary>Gets a value indicating whether the parameters may be edited.</summary>
    public bool IsIdle => !_isBusy;

    /// <summary>Gets a value indicating whether a scan, run, undo or export is in progress.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsIdle));
            RaisePlanProperties();
        }
    }

    /// <summary>Gets a value indicating whether the affix text goes before the name.</summary>
    public bool IsAffixPrefix => _affixPosition == RenameAffixPosition.Prefix;

    /// <summary>Gets a value indicating whether the affix text goes after the stem.</summary>
    public bool IsAffixSuffix => _affixPosition == RenameAffixPosition.Suffix;

    /// <summary>Gets or sets the basic task index.</summary>
    public int SelectedBasicTaskIndex
    {
        get => (int)_basicTask;
        set
        {
            int index = Math.Clamp(value, 0, 2);
            if ((int)_basicTask == index)
            {
                return;
            }

            _basicTask = (RenameBasicTask)index;
            OnBasicTaskChanged();
        }
    }

    /// <summary>Gets or sets the advanced rule kind index.</summary>
    public int SelectedAdvancedKindIndex
    {
        get => (int)_advancedKind;
        set
        {
            int index = Math.Clamp(value, 0, 1);
            if ((int)_advancedKind == index)
            {
                return;
            }

            _advancedKind = (RenameRuleKind)index;
            _advancedEdited = true;
            OnRuleChanged();
            OnPropertyChanged(nameof(ShowAdvancedReplaceFields));
            OnPropertyChanged(nameof(ShowAdvancedTemplateFields));
        }
    }

    /// <summary>Gets or sets the sequence zero-pad index.</summary>
    public int SelectedPadIndex
    {
        get => PadIndexOf(_sequencePadWidth);
        set
        {
            int index = Math.Clamp(value, 0, RenameRuleConverter.SequencePadWidths.Count - 1);
            int width = RenameRuleConverter.SequencePadWidths[index];
            if (_sequencePadWidth == width)
            {
                return;
            }

            _sequencePadWidth = width;
            OnRuleChanged();
        }
    }

    /// <summary>Gets the localized basic task names.</summary>
    public IReadOnlyList<string> BasicTaskNames =>
    [
        _localization["Tool.FileRename.Task.Replace"],
        _localization["Tool.FileRename.Task.Affix"],
        _localization["Tool.FileRename.Task.Sequence"],
    ];

    /// <summary>Gets the localized advanced rule names.</summary>
    public IReadOnlyList<string> AdvancedKindNames =>
    [
        _localization["Tool.FileRename.Rule.Replace"],
        _localization["Tool.FileRename.Rule.Template"],
    ];

    /// <summary>Gets the localized zero-pad choices.</summary>
    public IReadOnlyList<string> PadNames =>
    [
        _localization["Tool.FileRename.Task.Sequence.Pad.None"],
        _localization.Format("Tool.FileRename.Task.Sequence.Pad.Width", 2),
        _localization.Format("Tool.FileRename.Task.Sequence.Pad.Width", 3),
        _localization.Format("Tool.FileRename.Task.Sequence.Pad.Width", 4),
    ];

    /// <summary>Gets the placeholder of the sequence name box.</summary>
    public string SequenceNamePlaceholder => _localization["Tool.FileRename.Task.Sequence.DefaultName"];

    /// <summary>Gets or sets the basic literal text to find.</summary>
    public string BasicFind
    {
        get => _basicFind;
        set
        {
            if (SetProperty(ref _basicFind, value ?? string.Empty))
            {
                OnRuleChanged();
            }
        }
    }

    /// <summary>Gets or sets the basic literal replacement.</summary>
    public string BasicReplace
    {
        get => _basicReplace;
        set
        {
            if (SetProperty(ref _basicReplace, value ?? string.Empty))
            {
                OnRuleChanged();
            }
        }
    }

    /// <summary>Gets or sets the basic affix text.</summary>
    public string AffixText
    {
        get => _affixText;
        set
        {
            if (SetProperty(ref _affixText, value ?? string.Empty))
            {
                OnRuleChanged();
            }
        }
    }

    /// <summary>Gets or sets the basic sequence name. Empty uses the localized default.</summary>
    public string SequenceName
    {
        get => _sequenceName;
        set
        {
            if (SetProperty(ref _sequenceName, value ?? string.Empty))
            {
                OnRuleChanged();
            }
        }
    }

    /// <summary>Gets or sets the advanced search text or pattern.</summary>
    public string AdvancedFind
    {
        get => _advancedFind;
        set
        {
            if (SetProperty(ref _advancedFind, value ?? string.Empty))
            {
                _advancedEdited = true;
                OnRuleChanged();
            }
        }
    }

    /// <summary>Gets or sets the advanced replacement text.</summary>
    public string AdvancedReplace
    {
        get => _advancedReplace;
        set
        {
            if (SetProperty(ref _advancedReplace, value ?? string.Empty))
            {
                _advancedEdited = true;
                OnRuleChanged();
            }
        }
    }

    /// <summary>Gets or sets the advanced whole-name template.</summary>
    public string Template
    {
        get => _template;
        set
        {
            if (SetProperty(ref _template, value ?? string.Empty))
            {
                _advancedEdited = true;
                OnRuleChanged();
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether the advanced search text is a regular expression.</summary>
    public bool AdvancedUseRegex
    {
        get => _advancedUseRegex;
        set
        {
            if (SetProperty(ref _advancedUseRegex, value))
            {
                _advancedEdited = true;
                OnRuleChanged();
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether subdirectories are scanned.</summary>
    public bool IncludeSubdirectories
    {
        get => _includeSubdirectories;
        set
        {
            if (SetProperty(ref _includeSubdirectories, value))
            {
                OnScopeChanged();
            }
        }
    }

    /// <summary>Gets or sets the extension filter text.</summary>
    public string Extensions
    {
        get => _extensions;
        set
        {
            if (SetProperty(ref _extensions, value ?? string.Empty))
            {
                OnScopeChanged();
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether a safety copy is made before renaming.</summary>
    public bool BackupEnabled
    {
        get => _backupEnabled;
        set
        {
            if (SetProperty(ref _backupEnabled, value))
            {
                OnPropertyChanged(nameof(ShowConfirmBackup));
                ScheduleSave();
            }
        }
    }

    /// <summary>Gets a value indicating whether the confirmation shows the backup note.</summary>
    public bool ShowConfirmBackup => _backupEnabled;

    /// <summary>Gets a value indicating whether the preview has rows.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Gets a value indicating whether the empty inline text replaces the list.</summary>
    public bool ShowListEmpty => Rows.Count == 0;

    /// <summary>Gets the empty inline text for the current rule and target.</summary>
    public string ListEmptyText
    {
        get
        {
            if (!HasRuleInput)
            {
                return _localization[_level == RenameRuleLevel.Basic
                    ? _basicTask == RenameBasicTask.Affix
                        ? "Tool.FileRename.Empty.Hint.Affix"
                        : "Tool.FileRename.Empty.Hint.Basic"
                    : _advancedKind == RenameRuleKind.Template
                        ? "Tool.FileRename.Empty.Hint.Template"
                        : "Tool.FileRename.Empty.Hint.Advanced"];
            }

            return _localization[_target == RenameTarget.Files
                ? "Tool.FileRename.List.Empty.File"
                : "Tool.FileRename.List.Empty.Folder"];
        }
    }

    /// <summary>Gets the footer statistics for the current state.</summary>
    public string StatsText
    {
        get
        {
            if (_batchVisible && _lastBatch is { } batch)
            {
                if (batch.IsUndone)
                {
                    return batch.UndoFailedCount > 0
                        ? _localization.Format("Tool.FileRename.Stats.Undone.Failed", batch.UndoneCount, batch.UndoFailedCount)
                        : _localization.Format("Tool.FileRename.Stats.Undone", batch.UndoneCount);
                }

                return batch.FailedCount > 0
                    ? _localization.Format("Tool.FileRename.Stats.Done.Failed", batch.RenamedCount, batch.FailedCount)
                    : _localization.Format("Tool.FileRename.Stats.Done", batch.RenamedCount);
            }

            return _localization.Format(
                "Tool.FileRename.Stats.Pending",
                _plan.RenamableCount,
                _plan.UnmatchedCount);
        }
    }

    /// <summary>Gets a value indicating whether the skip button is shown.</summary>
    public bool HasSkipItems => SkippedRows.Count > 0;

    /// <summary>Gets the skip button label.</summary>
    public string SkipCountText => _localization.Format("Tool.FileRename.Skip.Toggle", SkippedRows.Count);

    /// <summary>Gets or sets a value indicating whether the skip panel is expanded.</summary>
    public bool IsSkipPanelOpen
    {
        get => _isSkipPanelOpen;
        private set
        {
            if (SetProperty(ref _isSkipPanelOpen, value))
            {
                OnPropertyChanged(nameof(IsSkipPanelVisible));
            }
        }
    }

    /// <summary>Gets a value indicating whether the skip panel is shown.</summary>
    public bool IsSkipPanelVisible => IsSkipPanelOpen && HasSkipItems;

    /// <summary>Gets a value indicating whether precheck blocked at least one row.</summary>
    public bool HasPrecheckErrors => !_batchVisible && _plan.HasConflicts;

    /// <summary>Gets the precheck summary line.</summary>
    public string PrecheckSummaryText =>
        _localization.Format("Tool.FileRename.Precheck.Summary", _plan.ConflictCount);

    /// <summary>Gets the distinct precheck reasons joined for the summary.</summary>
    public string PrecheckReasonText => string.Join(
        _localization["Tool.FileRename.Precheck.Separator"],
        _plan.DistinctErrors.Select(FileRenameText.ReasonKey).Select(key => _localization[key]));

    /// <summary>Gets a value indicating whether the batch may run.</summary>
    public bool CanRun =>
        !_isBusy &&
        !_isScanning &&
        _selectedDirectory is not null &&
        _scan is not null &&
        _plan.CanExecute &&
        !(_batchVisible && _lastBatch is { IsUndone: false, RenamedCount: > 0 });

    /// <summary>Gets a value indicating whether the last batch may be undone.</summary>
    public bool CanUndo => !_isBusy && _lastBatch is { IsUndone: false, RenamedCount: > 0 };

    /// <summary>Gets a value indicating whether the last batch may be exported.</summary>
    public bool CanExport => !_isBusy && _lastBatch is not null;

    /// <summary>Gets the confirmation title.</summary>
    public string ConfirmTitle => _localization["Tool.FileRename.Confirm.Title"];

    /// <summary>Gets the confirmation body for the current level.</summary>
    public string ConfirmBody => _level == RenameRuleLevel.Advanced
        ? _localization.Format(
            "Tool.FileRename.Confirm.Body.Advanced",
            _plan.RenamableCount,
            _plan.ExtensionChangeCount)
        : _localization.Format("Tool.FileRename.Confirm.Body", _plan.RenamableCount);

    /// <summary>Gets the confirmation note.</summary>
    public string ConfirmNote => _localization["Tool.FileRename.Confirm.Note"];

    /// <summary>Gets the confirmation backup note.</summary>
    public string ConfirmBackupText => _localization["Tool.FileRename.Confirm.Backup"];

    /// <summary>Gets the run action label.</summary>
    public string RunActionText => _localization["Tool.FileRename.Action.Run"];

    /// <summary>Gets the cancel label.</summary>
    public string CancelText => _localization["Common.Cancel"];

    /// <summary>Gets the suggested file name of the exported log.</summary>
    public string DefaultExportFileName
    {
        get
        {
            DateTimeOffset timestamp = _lastBatch?.ExecutedAt ?? _timeProvider.GetLocalNow();
            return "JiuMi-rename-" +
                timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) +
                ".json";
        }
    }

    /// <summary>Gets the file type label of the log save dialog.</summary>
    public string ExportFileTypeName => _localization["Tool.FileRename.Action.ExportFileType"];

    /// <summary>Gets the command that switches files and folders.</summary>
    public IRelayCommand<RenameTarget> SetTargetCommand
    {
        get;
    }

    /// <summary>Gets the command that switches the basic and advanced levels.</summary>
    public IRelayCommand<RenameRuleLevel> SetLevelCommand
    {
        get;
    }

    /// <summary>Gets the command that chooses the affix position.</summary>
    public IRelayCommand<RenameAffixPosition> SetAffixPositionCommand
    {
        get;
    }

    /// <summary>Gets the command that expands or collapses the skip panel.</summary>
    public IRelayCommand ToggleSkipCommand
    {
        get;
    }

    /// <summary>Gets the command that restores the last batch.</summary>
    public IAsyncRelayCommand UndoCommand
    {
        get;
    }

    /// <summary>
    /// Scans a folder and shows its first preview.
    /// </summary>
    /// <param name="path">The chosen folder; missing folders are ignored.</param>
    /// <returns>A task that completes when the preview is rebuilt.</returns>
    public async Task SetDirectoryAsync(string path)
    {
        if (_disposed || _isBusy || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim().Trim('"'));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            _logger.Warning(exception, "The chosen rename folder is not a valid path");
            return;
        }

        if (!_pathProbe.IsDirectory(fullPath))
        {
            _logger.Warning("The chosen rename folder {Directory} does not exist", fullPath);
            return;
        }

        _selectedDirectory = fullPath;
        _batchVisible = false;
        _scanStale = false;
        OnPropertyChanged(nameof(HasDirectory));
        OnPropertyChanged(nameof(ShowEmptyState));
        await RescanAsync();
    }

    /// <summary>
    /// Checks whether a dropped path is a folder, so the view does not probe the file system itself.
    /// </summary>
    /// <param name="path">The dropped path.</param>
    /// <returns><see langword="true"/> when the path is an existing folder.</returns>
    public bool IsDirectory(string path) =>
        !string.IsNullOrWhiteSpace(path) && _pathProbe.IsDirectory(path);

    /// <summary>
    /// Records a failure raised by a page action without breaking the page.
    /// </summary>
    /// <param name="exception">The picker, dialog, drop or export failure.</param>
    public void ReportPageFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _logger.Warning(exception, "A rename page action failed");
    }

    /// <summary>
    /// Runs the confirmed batch.
    /// </summary>
    /// <returns>A task that completes when the batch and the summary are shown.</returns>
    public async Task ExecuteAsync()
    {
        if (!CanRun || _selectedDirectory is null)
        {
            return;
        }

        RenamePlanEntry[] entries = [.. _plan.Entries.Where(entry => entry.CanRename)];
        if (entries.Length == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var request = new RenameExecutionRequest(
                _selectedDirectory,
                [
                    .. entries.Select(entry => new RenameExecutionItem(
                        entry.Candidate.FullPath,
                        entry.TargetFullPath,
                        entry.Candidate.IsDirectory)),
                ],
                _backupEnabled);
            RenameOperationResult result = await _executor.ExecuteAsync(
                request,
                new Progress<RenameItemOutcome>(ApplyOutcome),
                _lifetime.Token);
            var outcomes = result.Items.ToDictionary(
                item => item.SourcePath,
                StringComparer.OrdinalIgnoreCase);
            var batchEntries = new List<RenameBatchEntry>(entries.Length);
            int renamed = 0;
            int failed = 0;
            foreach (RenamePlanEntry entry in entries)
            {
                string source = entry.Candidate.FullPath;
                if (!outcomes.TryGetValue(source, out RenameItemOutcome? outcome))
                {
                    outcome = new RenameItemOutcome(
                        source,
                        entry.TargetFullPath,
                        entry.Candidate.IsDirectory,
                        RenameFailure.Unknown);
                }

                FileRenameRowViewModel? row = FindRow(source);
                if (outcome.Succeeded)
                {
                    row?.MarkDone();
                    renamed++;
                    batchEntries.Add(new RenameBatchEntry(
                        source,
                        entry.TargetFullPath,
                        entry.Candidate.IsDirectory,
                        outcome.Stamp,
                        RenameBatchEntryState.Renamed));
                }
                else
                {
                    row?.MarkFailed(outcome.Failure ?? RenameFailure.Unknown);
                    failed++;
                    batchEntries.Add(new RenameBatchEntry(
                        source,
                        entry.TargetFullPath,
                        entry.Candidate.IsDirectory,
                        Stamp: null,
                        RenameBatchEntryState.Failed,
                        outcome.Failure));
                    ReportItemFailure(source, "FileRename.Execute", outcome.Failure ?? RenameFailure.Unknown);
                }
            }

            _lastBatch = new RenameBatch(
                _timeProvider.GetLocalNow(),
                _backupEnabled,
                result.BackupDirectory,
                batchEntries);
            _batchVisible = true;
            _scanStale = true;
            ShowExecutionSummary(renamed, failed);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "The rename batch failed");
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.FileRenameFailed,
                "FileRename.Execute",
                exception));
            _infoBar.ShowSummary(new InfoBarSummary(
                _localization["Tool.FileRename.InfoBar.Failed"],
                IsWarning: true,
                FolderPath: null));
        }
        finally
        {
            IsBusy = false;
            RaisePlanProperties();
        }
    }

    /// <summary>
    /// Restores the last batch in reverse order, verifying each file first.
    /// </summary>
    /// <returns>A task that completes when the undo and the summary are shown.</returns>
    public async Task UndoAsync()
    {
        if (!CanUndo || _lastBatch is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            RenameUndoItem[] items =
            [
                .. _lastBatch.Entries
                    .Where(entry => entry.State == RenameBatchEntryState.Renamed)
                    .Select(entry => new RenameUndoItem(
                        entry.OriginalPath,
                        entry.RenamedPath,
                        entry.IsDirectory,
                        entry.Stamp)),
            ];
            RenameOperationResult result = await _executor.UndoAsync(items, _lifetime.Token);
            var outcomes = result.Items.ToDictionary(
                item => item.SourcePath,
                StringComparer.OrdinalIgnoreCase);
            var entries = new List<RenameBatchEntry>(_lastBatch.Entries.Count);
            int undone = 0;
            int failed = 0;
            foreach (RenameBatchEntry entry in _lastBatch.Entries)
            {
                if (entry.State != RenameBatchEntryState.Renamed ||
                    !outcomes.TryGetValue(entry.OriginalPath, out RenameItemOutcome? outcome))
                {
                    entries.Add(entry);
                    continue;
                }

                FileRenameRowViewModel? row = FindRow(entry.OriginalPath);
                if (outcome.Succeeded)
                {
                    row?.MarkPending();
                    undone++;
                    entries.Add(entry with
                    {
                        State = RenameBatchEntryState.Undone,
                        Failure = null,
                    });
                }
                else
                {
                    row?.MarkUndoFailed(outcome.Failure ?? RenameFailure.Unknown);
                    failed++;
                    entries.Add(entry with
                    {
                        State = RenameBatchEntryState.UndoFailed,
                        Failure = outcome.Failure,
                    });
                    ReportItemFailure(entry.OriginalPath, "FileRename.Undo", outcome.Failure ?? RenameFailure.Unknown);
                }
            }

            _lastBatch = _lastBatch with
            {
                Entries = entries,
            };
            _batchVisible = true;
            _scanStale = true;
            ShowUndoSummary(undone, failed);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Undoing the rename batch failed");
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.FileRenameFailed,
                "FileRename.Undo",
                exception));
            _infoBar.ShowSummary(new InfoBarSummary(
                _localization["Tool.FileRename.InfoBar.Failed"],
                IsWarning: true,
                FolderPath: null));
        }
        finally
        {
            IsBusy = false;
            RaisePlanProperties();
        }
    }

    /// <summary>
    /// Writes the last batch log to the chosen path.
    /// </summary>
    /// <param name="path">The destination chosen in the save dialog.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public async Task ExportLogAsync(string path)
    {
        if (_lastBatch is null)
        {
            return;
        }

        try
        {
            await _logWriter.WriteAsync(path, _lastBatch, _lifetime.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Exporting the rename log failed");
            _errorReporter.Report(_recordFactory.Create(
                ErrorCodes.FileRenameFailed,
                "FileRename.ExportLog",
                exception));
            _infoBar.ShowSummary(new InfoBarSummary(
                _localization["Tool.FileRename.InfoBar.ExportFailed"],
                IsWarning: true,
                FolderPath: null));
        }
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
        _saveTimer.Stop();
        _saveTimer.Tick -= OnSaveTick;
        _scanTimer.Stop();
        _scanTimer.Tick -= OnScanTick;
        _localization.PropertyChanged -= OnLocalizationChanged;
        PersistSettings();
        _lifetime.Dispose();
    }

    private void SetTarget(RenameTarget target)
    {
        if (_target == target)
        {
            return;
        }

        _target = target;
        OnPropertyChanged(nameof(IsFilesMode));
        OnPropertyChanged(nameof(IsFoldersMode));
        OnPropertyChanged(nameof(ShowExtensionFilter));
        OnScopeChanged();
    }

    private void SetLevel(RenameRuleLevel level)
    {
        if (_level == level)
        {
            return;
        }

        _level = level;
        if (level == RenameRuleLevel.Advanced && !_advancedEdited)
        {
            ConvertBasicToAdvanced();
        }

        OnPropertyChanged(nameof(IsBasicLevel));
        OnPropertyChanged(nameof(IsAdvancedLevel));
        OnPropertyChanged(nameof(ShowBasicReplaceFields));
        OnPropertyChanged(nameof(ShowBasicAffixFields));
        OnPropertyChanged(nameof(ShowBasicSequenceFields));
        OnPropertyChanged(nameof(ShowAdvancedReplaceFields));
        OnPropertyChanged(nameof(ShowAdvancedTemplateFields));
        OnRuleChanged();
    }

    private void SetAffixPosition(RenameAffixPosition position)
    {
        if (_affixPosition == position)
        {
            return;
        }

        _affixPosition = position;
        OnPropertyChanged(nameof(IsAffixPrefix));
        OnPropertyChanged(nameof(IsAffixSuffix));
        OnRuleChanged();
    }

    private void ConvertBasicToAdvanced()
    {
        if (_basicTask == RenameBasicTask.Replace)
        {
            _advancedKind = RenameRuleKind.Replace;
            _advancedFind = _basicFind;
            _advancedReplace = _basicReplace;
            _advancedUseRegex = false;
        }
        else
        {
            RenameRule template = RenameRuleConverter.FromBasic(
                _basicTask,
                _basicFind,
                _basicReplace,
                _affixPosition,
                _affixText,
                EffectiveSequenceName,
                _sequencePadWidth);
            _advancedKind = RenameRuleKind.Template;
            _template = template.Template;
        }

        OnPropertyChanged(nameof(SelectedAdvancedKindIndex));
        OnPropertyChanged(nameof(AdvancedFind));
        OnPropertyChanged(nameof(AdvancedReplace));
        OnPropertyChanged(nameof(AdvancedUseRegex));
        OnPropertyChanged(nameof(Template));
        OnPropertyChanged(nameof(ShowAdvancedReplaceFields));
        OnPropertyChanged(nameof(ShowAdvancedTemplateFields));
    }

    private void OnBasicTaskChanged()
    {
        OnPropertyChanged(nameof(ShowBasicReplaceFields));
        OnPropertyChanged(nameof(ShowBasicAffixFields));
        OnPropertyChanged(nameof(ShowBasicSequenceFields));
        OnRuleChanged();
    }

    private void OnRuleChanged()
    {
        _batchVisible = false;
        if (_scanStale)
        {
            _scanStale = false;
            _ = RescanAsync();
        }
        else
        {
            RebuildPlan();
        }

        ScheduleSave();
    }

    private void OnScopeChanged()
    {
        _batchVisible = false;
        ScheduleRescan();
        ScheduleSave();
    }

    private void ScheduleRescan()
    {
        if (_selectedDirectory is null)
        {
            return;
        }

        _isScanning = true;
        RaisePlanProperties();
        _scanTimer.Stop();
        _scanTimer.Start();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnScanTick(object? sender, EventArgs e)
    {
        _scanTimer.Stop();
        _ = RescanAsync();
    }

    private void OnSaveTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        PersistSettings();
    }

    private async Task RescanAsync()
    {
        if (_disposed || _selectedDirectory is null)
        {
            return;
        }

        int version = ++_scanVersion;
        _isScanning = true;
        RaisePlanProperties();
        try
        {
            RenameScanResult result;
            try
            {
                result = await _scanner.ScanAsync(
                    new RenameScanRequest(
                        _selectedDirectory,
                        _target,
                        _includeSubdirectories,
                        _extensions),
                    _lifetime.Token);
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "Scanning the rename folder failed");
                result = new RenameScanResult([]);
            }

            if (_disposed || version != _scanVersion)
            {
                return;
            }

            _isScanning = false;
            _scan = result;
            _scanStale = false;
            RebuildPlan();
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Building the rename preview failed");
            if (version == _scanVersion)
            {
                _isScanning = false;
                RaisePlanProperties();
            }
        }
    }

    private void RebuildPlan()
    {
        if (_selectedDirectory is null || _scan is null)
        {
            _plan = new RenamePlan([], [], 0);
            PopulateRows();
            RaisePlanProperties();
            return;
        }

        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        _plan = RenamePlanner.Build(
            BuildRule(),
            _scan.Candidates,
            today,
            _pathProbe.Exists);
        PopulateRows();
        RaisePlanProperties();
    }

    private RenameRule BuildRule() => _level == RenameRuleLevel.Basic
        ? RenameRuleConverter.FromBasic(
            _basicTask,
            _basicFind,
            _basicReplace,
            _affixPosition,
            _affixText,
            EffectiveSequenceName,
            _sequencePadWidth)
        : _advancedKind == RenameRuleKind.Template
            ? RenameRule.CreateTemplate(_template)
            : RenameRule.CreateReplace(_advancedFind, _advancedReplace, _advancedUseRegex);

    private string EffectiveSequenceName => string.IsNullOrWhiteSpace(_sequenceName)
        ? _localization["Tool.FileRename.Task.Sequence.DefaultName"]
        : _sequenceName;

    private bool HasRuleInput => _level == RenameRuleLevel.Basic
        ? _basicTask switch
        {
            RenameBasicTask.Replace => _basicFind.Length > 0,
            RenameBasicTask.Affix => _affixText.Length > 0,
            _ => true,
        }
        : _advancedKind == RenameRuleKind.Replace
            ? _advancedFind.Length > 0
            : _template.Length > 0;

    private void PopulateRows()
    {
        Rows.Clear();
        bool folders = _target == RenameTarget.Folders;
        for (int index = 0; index < _plan.Entries.Count; index++)
        {
            Rows.Add(new FileRenameRowViewModel(
                _plan.Entries[index],
                folders,
                isLast: index == _plan.Entries.Count - 1,
                _localization));
        }

        SkippedRows.Clear();
        foreach (RenameSkippedItem item in _plan.Skipped)
        {
            SkippedRows.Add(new FileRenameSkipRowViewModel(item, _localization));
        }
    }

    private FileRenameRowViewModel? FindRow(string originalPath)
    {
        foreach (FileRenameRowViewModel row in Rows)
        {
            if (string.Equals(row.OriginalPath, originalPath, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    private void ApplyOutcome(RenameItemOutcome outcome)
    {
        FileRenameRowViewModel? row = FindRow(outcome.SourcePath);
        if (outcome.Succeeded)
        {
            row?.MarkDone();
        }
        else
        {
            row?.MarkFailed(outcome.Failure ?? RenameFailure.Unknown);
        }
    }

    private void ShowExecutionSummary(int renamed, int failed)
    {
        _infoBar.ShowSummary(failed == 0
            ? new InfoBarSummary(
                _localization.Format("Tool.FileRename.InfoBar.Done", renamed),
                IsWarning: false,
                _selectedDirectory)
            {
                ActionText = _localization["Tool.FileRename.Action.OpenFolder"],
            }
            : new InfoBarSummary(
                _localization.Format("Tool.FileRename.InfoBar.Partial", renamed, failed),
                IsWarning: true,
                FolderPath: null)
            {
                SecondaryMessage = _localization["Tool.FileRename.InfoBar.Partial.Detail"],
                ActionText = _localization["Tool.FileRename.Action.Export"],
                Action = ExportLogToDefaultPathAsync,
            });
    }

    private void ShowUndoSummary(int undone, int failed)
    {
        _infoBar.ShowSummary(failed == 0
            ? new InfoBarSummary(
                _localization.Format("Tool.FileRename.InfoBar.Undone", undone),
                IsWarning: false,
                _selectedDirectory)
            {
                ActionText = _localization["Tool.FileRename.Action.OpenFolder"],
            }
            : new InfoBarSummary(
                _localization.Format("Tool.FileRename.InfoBar.UndoFailed", failed),
                IsWarning: true,
                FolderPath: null)
            {
                SecondaryMessage = _localization["Tool.FileRename.InfoBar.UndoFailed.Detail"],
                ActionText = _localization["Tool.FileRename.Action.Export"],
                Action = ExportLogToDefaultPathAsync,
            });
    }

    private async Task ExportLogToDefaultPathAsync()
    {
        if (_lastBatch is null || _selectedDirectory is null)
        {
            return;
        }

        await ExportLogAsync(Path.Combine(_selectedDirectory, DefaultExportFileName));
    }

    private void ReportItemFailure(string sourcePath, string operation, RenameFailure failure)
    {
        _errorReporter.Report(_recordFactory.Create(
            ErrorCodes.FileRenameFailed,
            operation,
            error: null,
            level: ErrorLevel.Warning));
        _logger.Warning(
            "Renaming {SourcePath} failed during {Operation} with {Failure}",
            sourcePath,
            operation,
            failure);
    }

    private void PersistSettings()
    {
        SettingsSaveResult result = _settingsStore.Save(_settingsStore.Current with
        {
            FileRename = new FileRenameSettings
            {
                RuleLevel = _level,
                BasicTask = _basicTask,
                BasicFind = _basicFind,
                BasicReplace = _basicReplace,
                AffixPosition = _affixPosition,
                AffixText = _affixText,
                SequenceName = _sequenceName,
                SequencePadWidth = _sequencePadWidth,
                AdvancedKind = _advancedKind,
                AdvancedFind = _advancedFind,
                AdvancedReplace = _advancedReplace,
                AdvancedUseRegex = _advancedUseRegex,
                Template = _template,
                AdvancedEdited = _advancedEdited,
                IncludeSubdirectories = _includeSubdirectories,
                Extensions = _extensions,
                BackupEnabled = _backupEnabled,
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

    private void RaisePlanProperties()
    {
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(ShowListEmpty));
        OnPropertyChanged(nameof(ListEmptyText));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(SkipCountText));
        OnPropertyChanged(nameof(HasSkipItems));
        OnPropertyChanged(nameof(IsSkipPanelVisible));
        OnPropertyChanged(nameof(HasPrecheckErrors));
        OnPropertyChanged(nameof(PrecheckSummaryText));
        OnPropertyChanged(nameof(PrecheckReasonText));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(ConfirmBody));
        OnPropertyChanged(nameof(ShowConfirmBackup));
        UndoCommand.NotifyCanExecuteChanged();
    }

    private static int PadIndexOf(int width)
    {
        for (int index = 0; index < RenameRuleConverter.SequencePadWidths.Count; index++)
        {
            if (RenameRuleConverter.SequencePadWidths[index] == width)
            {
                return index;
            }
        }

        return 0;
    }

    private void ToggleSkip() => IsSkipPanelOpen = !IsSkipPanelOpen;

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(
            e.PropertyName,
            nameof(ILocalizationService.CurrentLanguage),
            StringComparison.Ordinal))
        {
            return;
        }

        foreach (FileRenameRowViewModel row in Rows)
        {
            row.RefreshText();
        }

        foreach (FileRenameSkipRowViewModel row in SkippedRows)
        {
            row.RefreshText();
        }

        OnPropertyChanged(nameof(BasicTaskNames));
        OnPropertyChanged(nameof(AdvancedKindNames));
        OnPropertyChanged(nameof(PadNames));
        OnPropertyChanged(nameof(SequenceNamePlaceholder));
        OnPropertyChanged(nameof(ListEmptyText));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(PrecheckSummaryText));
        OnPropertyChanged(nameof(PrecheckReasonText));
        OnPropertyChanged(nameof(ConfirmTitle));
        OnPropertyChanged(nameof(ConfirmBody));
        OnPropertyChanged(nameof(ConfirmNote));
        OnPropertyChanged(nameof(ConfirmBackupText));
        OnPropertyChanged(nameof(RunActionText));
        OnPropertyChanged(nameof(CancelText));
        OnPropertyChanged(nameof(ExportFileTypeName));
    }
}
