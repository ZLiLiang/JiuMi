namespace JiuMi.Application.Errors;

/// <summary>
/// What the single error window shows for one failure.
/// </summary>
/// <param name="Record">The recorded failure.</param>
/// <param name="CanContinue">Whether the main window is usable, so the window may offer continuing.</param>
/// <param name="DetailsText">
/// The text the copy action places on the clipboard; it comes from the same formatter as the info bar,
/// so its first line is the localized <c>Error.&lt;code&gt;</c> text.
/// </param>
public sealed record ErrorWindowRequest(ErrorRecord Record, bool CanContinue, string DetailsText)
{
    /// <summary>
    /// Gets the action the default button, Enter, Esc and the close button perform.
    /// </summary>
    public ErrorWindowAction DefaultAction =>
        CanContinue ? ErrorWindowAction.Continue : ErrorWindowAction.Exit;
}
