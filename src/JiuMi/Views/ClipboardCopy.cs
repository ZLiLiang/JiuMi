using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace JiuMi.Views;

/// <summary>
/// Writes text to the desktop clipboard for the surfaces that offer "copy details".
/// </summary>
internal static class ClipboardCopy
{
    /// <summary>
    /// Tries to place <paramref name="text"/> on the clipboard.
    /// </summary>
    /// <param name="topLevel">The top level whose clipboard is used.</param>
    /// <param name="text">The text to copy.</param>
    /// <returns>Whether the text was copied, and the failure when another process held the clipboard.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The clipboard is owned by the desktop; a failed copy is reported, not thrown at the UI.")]
    public static async Task<Outcome> TrySetTextAsync(TopLevel? topLevel, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (topLevel?.Clipboard is not { } clipboard)
        {
            return Outcome.Unavailable;
        }

        try
        {
            await clipboard.SetTextAsync(text);
            return Outcome.Success;
        }
        catch (Exception error)
        {
            return new Outcome(Copied: false, error);
        }
    }

    /// <summary>
    /// What happened when the clipboard was asked to take the text.
    /// </summary>
    /// <param name="Copied">Whether the clipboard now holds the text.</param>
    /// <param name="Error">The failure, when the clipboard rejected the text.</param>
    internal readonly record struct Outcome(bool Copied, Exception? Error)
    {
        /// <summary>
        /// Gets the outcome of a copy that reached the clipboard.
        /// </summary>
        public static Outcome Success
        {
            get;
        } = new(Copied: true, Error: null);

        /// <summary>
        /// Gets the outcome of a copy attempt made without a clipboard.
        /// </summary>
        public static Outcome Unavailable
        {
            get;
        } = new(Copied: false, Error: null);
    }
}
