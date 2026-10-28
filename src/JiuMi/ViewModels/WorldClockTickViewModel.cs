namespace JiuMi.ViewModels;

/// <summary>
/// One hour label of the plan axis; odd hours leave the text empty.
/// </summary>
/// <param name="Text">The label, or an empty string for an unlabelled hour.</param>
public sealed record WorldClockTickViewModel(string Text);
