using JiuMi.Application.FileRename;
using JiuMi.Application.Localization;

namespace JiuMi.ViewModels;

/// <summary>
/// One row of the rename preview: the original name, the highlighted change and the row status.
/// </summary>
public sealed class FileRenameRowViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;
    private readonly RenamePrecheckError? _precheckError;
    private readonly string _originalPath;
    private readonly string _targetPath;
    private readonly RenameTextDiff _diff;
    private FileRenameRowStatus _status;
    private RenameFailure? _failure;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenameRowViewModel"/> class.
    /// </summary>
    /// <param name="entry">The plan row.</param>
    /// <param name="useRelativePath">Whether the original column shows the path below the selected folder.</param>
    /// <param name="isLast">Whether this is the last row, which omits the bottom divider.</param>
    /// <param name="localization">Supplies the row text.</param>
    public FileRenameRowViewModel(
        RenamePlanEntry entry,
        bool useRelativePath,
        bool isLast,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(localization);

        _localization = localization;
        _precheckError = entry.Error;
        _originalPath = entry.Candidate.FullPath;
        _targetPath = entry.TargetFullPath;
        IsLast = isLast;
        OriginalDisplay = useRelativePath ? entry.Candidate.RelativePath : entry.OriginalName;
        TargetDisplay = entry.TargetName;
        _diff = RenameTextDiff.Compute(OriginalDisplay, TargetDisplay);
        _status = entry.Error is null ? FileRenameRowStatus.Pending : FileRenameRowStatus.Conflict;
    }

    /// <summary>Gets a value indicating whether this is the last row of the list.</summary>
    public bool IsLast
    {
        get;
    }

    /// <summary>Gets the original name, or the relative path in folder mode.</summary>
    public string OriginalDisplay
    {
        get;
    }

    /// <summary>Gets the new name.</summary>
    public string TargetDisplay
    {
        get;
    }

    /// <summary>Gets the unchanged start of the new name.</summary>
    public string DiffPrefix => _diff.Prefix;

    /// <summary>Gets the changed middle of the new name.</summary>
    public string DiffChanged => _diff.Changed;

    /// <summary>Gets the unchanged end of the new name.</summary>
    public string DiffSuffix => _diff.Suffix;

    /// <summary>Gets the original absolute path.</summary>
    public string OriginalPath => _originalPath;

    /// <summary>Gets the target absolute path.</summary>
    public string TargetPath => _targetPath;

    /// <summary>Gets the row status.</summary>
    public FileRenameRowStatus Status => _status;

    /// <summary>Gets a value indicating whether the row will be renamed.</summary>
    public bool IsPending => _status == FileRenameRowStatus.Pending;

    /// <summary>Gets a value indicating whether precheck blocks the row.</summary>
    public bool IsConflict => _status == FileRenameRowStatus.Conflict;

    /// <summary>Gets a value indicating whether the row was renamed.</summary>
    public bool IsDone => _status == FileRenameRowStatus.Done;

    /// <summary>Gets a value indicating whether the row failed to rename.</summary>
    public bool IsFailed => _status == FileRenameRowStatus.Failed;

    /// <summary>Gets a value indicating whether undo failed for the row.</summary>
    public bool IsUndoFailed => _status == FileRenameRowStatus.UndoFailed;

    /// <summary>Gets a value indicating whether the row uses the critical status chip.</summary>
    public bool IsFailedOrUndoFailed =>
        _status is FileRenameRowStatus.Failed or FileRenameRowStatus.UndoFailed;

    /// <summary>Gets a value indicating whether the target name is shown in the critical colour.</summary>
    public bool TargetIsInvalid =>
        _status is FileRenameRowStatus.Conflict or FileRenameRowStatus.Failed or FileRenameRowStatus.UndoFailed;

    /// <summary>Gets the localized status label.</summary>
    public string StatusText => _localization[_status switch
    {
        FileRenameRowStatus.Conflict => "Tool.FileRename.Status.Conflict",
        FileRenameRowStatus.Done => "Tool.FileRename.Status.Done",
        FileRenameRowStatus.Failed => "Tool.FileRename.Status.Failed",
        FileRenameRowStatus.UndoFailed => "Tool.FileRename.Status.UndoFailed",
        _ => "Tool.FileRename.Status.Pending",
    }];

    /// <summary>Gets the localized reason below the target, or an empty string.</summary>
    public string ReasonText
    {
        get
        {
            if (_failure is { } failure)
            {
                return _localization[failure switch
                {
                    RenameFailure.SourceMissing => "Tool.FileRename.Failure.SourceMissing",
                    RenameFailure.TargetExists => "Tool.FileRename.Failure.TargetExists",
                    RenameFailure.AccessDenied => "Tool.FileRename.Failure.AccessDenied",
                    RenameFailure.BackupFailed => "Tool.FileRename.Failure.BackupFailed",
                    RenameFailure.Modified => "Tool.FileRename.Failure.Modified",
                    _ => "Tool.FileRename.Failure.Unknown",
                }];
            }

            return _precheckError is { } error
                ? _localization[FileRenameText.ReasonKey(error)]
                : string.Empty;
        }
    }

    /// <summary>Gets a value indicating whether a reason is shown.</summary>
    public bool HasReason => ReasonText.Length > 0;

    /// <summary>Marks the row as renamed.</summary>
    public void MarkDone()
    {
        _failure = null;
        SetStatus(FileRenameRowStatus.Done);
    }

    /// <summary>Marks the row as failed to rename.</summary>
    /// <param name="failure">Why the rename failed.</param>
    public void MarkFailed(RenameFailure failure)
    {
        _failure = failure;
        SetStatus(FileRenameRowStatus.Failed);
    }

    /// <summary>Marks the row as not restored by undo.</summary>
    /// <param name="failure">Why undo failed.</param>
    public void MarkUndoFailed(RenameFailure failure)
    {
        _failure = failure;
        SetStatus(FileRenameRowStatus.UndoFailed);
    }

    /// <summary>Returns the row to the pending state after undo or a parameter change.</summary>
    public void MarkPending()
    {
        _failure = null;
        SetStatus(_precheckError is null ? FileRenameRowStatus.Pending : FileRenameRowStatus.Conflict);
    }

    /// <summary>Recomputes every localized text after a language change.</summary>
    public void RefreshText()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ReasonText));
        OnPropertyChanged(nameof(HasReason));
    }

    private void SetStatus(FileRenameRowStatus status)
    {
        _status = status;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsConflict));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsUndoFailed));
        OnPropertyChanged(nameof(IsFailedOrUndoFailed));
        OnPropertyChanged(nameof(TargetIsInvalid));
        RefreshText();
    }
}
