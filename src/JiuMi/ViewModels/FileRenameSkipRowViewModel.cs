using JiuMi.Application.FileRename;
using JiuMi.Application.Localization;

namespace JiuMi.ViewModels;

/// <summary>
/// One row of the skipped panel: the name left out of the batch and why.
/// </summary>
public sealed class FileRenameSkipRowViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenameSkipRowViewModel"/> class.
    /// </summary>
    /// <param name="item">The skipped item.</param>
    /// <param name="localization">Supplies the reason text.</param>
    public FileRenameSkipRowViewModel(RenameSkippedItem item, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(localization);

        _localization = localization;
        Reason = item.Reason;
        DisplayName = item.Candidate.RelativePath;
    }

    /// <summary>Gets the name, or the relative path in folder mode.</summary>
    public string DisplayName
    {
        get;
    }

    /// <summary>Gets why the item is skipped.</summary>
    public RenameSkipReason Reason
    {
        get;
    }

    /// <summary>Gets the localized reason chip text.</summary>
    public string ReasonText => _localization[Reason switch
    {
        RenameSkipReason.System => "Tool.FileRename.Skip.System",
        RenameSkipReason.SymbolicLink => "Tool.FileRename.Skip.Link",
        RenameSkipReason.Junction => "Tool.FileRename.Skip.Junction",
        _ => "Tool.FileRename.Skip.Hidden",
    }];

    /// <summary>Recomputes the localized reason after a language change.</summary>
    public void RefreshText() => OnPropertyChanged(nameof(ReasonText));
}
