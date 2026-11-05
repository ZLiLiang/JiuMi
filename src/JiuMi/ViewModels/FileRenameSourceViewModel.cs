using System;
using System.Collections.Generic;
using System.IO;
using JiuMi.Application.FileRename;
using JiuMi.Application.Localization;

namespace JiuMi.ViewModels;

/// <summary>
/// One chip in the file-mode source row: what the source is and how much it added to the batch.
/// </summary>
public sealed class FileRenameSourceViewModel
{
    private const int TooltipFileLimit = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRenameSourceViewModel"/> class.
    /// </summary>
    /// <param name="source">The source the chip removes.</param>
    /// <param name="summary">What the last scan kept from the source, or <see langword="null"/> before it runs.</param>
    /// <param name="localization">Supplies the localized chip text.</param>
    public FileRenameSourceViewModel(
        RenameSource source,
        RenameSourceScanSummary? summary,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(localization);

        Order = source.Order;
        IsFolder = source.Kind == RenameSourceKind.Folder;
        Name = IsFolder
            ? FileRenameText.DirectoryLabel(source.Directory)
            : localization.Format("Tool.FileRename.Source.Files", source.Paths.Count);
        CountText = BuildCountText(summary, localization);
        ToolTipText = BuildToolTip(source, summary, localization);
        RemoveText = localization["Tool.FileRename.Source.Remove"];
    }

    /// <summary>Gets the one-based add order, which identifies the source in the page.</summary>
    public int Order
    {
        get;
    }

    /// <summary>Gets a value indicating whether the chip represents a folder scan.</summary>
    public bool IsFolder
    {
        get;
    }

    /// <summary>Gets the folder name, or the file count of a directly added group.</summary>
    public string Name
    {
        get;
    }

    /// <summary>Gets the net contribution, or the filtered ratio when the extension filter removed files.</summary>
    public string CountText
    {
        get;
    }

    /// <summary>Gets the full paths and any files the extension filter removed.</summary>
    public string ToolTipText
    {
        get;
    }

    /// <summary>Gets the remove action name.</summary>
    public string RemoveText
    {
        get;
    }

    private static string BuildCountText(RenameSourceScanSummary? summary, ILocalizationService localization)
    {
        if (summary is null)
        {
            return string.Empty;
        }

        return summary.FilteredCount > 0
            ? localization.Format("Tool.FileRename.Source.Filtered", summary.ContributionCount, summary.TotalCount)
            : localization.Format("Tool.FileRename.Source.Count", summary.ContributionCount);
    }

    private static string BuildToolTip(
        RenameSource source,
        RenameSourceScanSummary? summary,
        ILocalizationService localization)
    {
        var lines = new List<string>();
        if (source.Kind == RenameSourceKind.Folder)
        {
            lines.Add(source.Paths[0]);
        }
        else
        {
            if (source.Directory is { } directory)
            {
                lines.Add(directory);
            }

            List<string> names = [];
            foreach (string path in source.Paths)
            {
                names.Add(Path.GetFileName(path));
            }

            int shown = Math.Min(names.Count, TooltipFileLimit);
            for (int index = 0; index < shown; index++)
            {
                lines.Add(names[index]);
            }

            if (names.Count > shown)
            {
                lines.Add(localization.Format("Tool.FileRename.Source.More", names.Count - shown));
            }
        }

        if (summary is { FilteredCount: > 0 })
        {
            List<string> filtered = [];
            foreach (string path in summary.FilteredPaths)
            {
                filtered.Add(Path.GetFileName(path));
            }

            lines.Add(localization.Format(
                "Tool.FileRename.Source.Filtered.Tooltip",
                string.Join(", ", filtered)));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
