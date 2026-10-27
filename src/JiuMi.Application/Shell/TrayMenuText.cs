namespace JiuMi.Application.Shell;

/// <summary>
/// The localized labels of the tray context menu, in the order the menu shows them.
/// </summary>
/// <param name="Show">The label that brings the main window forward.</param>
/// <param name="Settings">The label that brings the window forward and opens the settings page.</param>
/// <param name="Exit">The label that closes the main window and ends the process.</param>
public sealed record TrayMenuText(string Show, string Settings, string Exit);
