using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace JiuMi.Views;

/// <summary>
/// Resolves a folder for a system picker's start location. A remembered directory that no longer
/// exists or cannot be opened is skipped silently.
/// </summary>
internal static class PickerStartLocation
{
    /// <summary>
    /// Sets a picker's suggested start location from a remembered directory, leaving the system
    /// default when the directory is missing or cannot be opened.
    /// </summary>
    /// <param name="options">The picker options to update.</param>
    /// <param name="topLevel">Supplies the storage provider that converts the path.</param>
    /// <param name="onFailure">Receives a directory that could not be converted.</param>
    /// <param name="candidate">The remembered directory.</param>
    /// <returns>A task that completes when the options are updated.</returns>
    public static async Task ApplyAsync(
        PickerOptions options,
        TopLevel topLevel,
        Action<Exception> onFailure,
        string? candidate)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (await ResolveAsync(topLevel, onFailure, candidate) is { } startLocation)
        {
            options.SuggestedStartLocation = startLocation;
        }
    }

    /// <summary>
    /// Returns the first candidate directory that exists and can be opened.
    /// </summary>
    /// <param name="topLevel">Supplies the storage provider that converts the path.</param>
    /// <param name="onFailure">Receives a directory that could not be converted; the next candidate is tried.</param>
    /// <param name="candidates">The directories to try, most preferred first.</param>
    /// <returns>The start folder, or <see langword="null"/> for the system default.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A picker start folder is a platform boundary; the failure is reported and the system default is used.")]
    public static async Task<IStorageFolder?> ResolveAsync(
        TopLevel topLevel,
        Action<Exception> onFailure,
        params string?[] candidates)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(onFailure);
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (string? candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
            {
                continue;
            }

            try
            {
                if (await topLevel.StorageProvider.TryGetFolderFromPathAsync(candidate) is { } folder)
                {
                    return folder;
                }
            }
            catch (Exception exception)
            {
                // The directory can disappear or become inaccessible between the check and the call.
                onFailure(exception);
            }
        }

        return null;
    }
}
