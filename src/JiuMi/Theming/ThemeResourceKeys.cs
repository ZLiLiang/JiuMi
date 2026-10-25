namespace JiuMi.Theming;

/// <summary>
/// Resource keys shared by the theme applier and the window backdrop.
/// </summary>
internal static class ThemeResourceKeys
{
    /// <summary>The solid surface brush used when Mica is unavailable.</summary>
    public const string SolidBackgroundBrush = "SolidBackgroundFillColorBaseBrush";

    /// <summary>The primary text brush, paired with the surface brush to detect a full palette.</summary>
    public const string PrimaryTextBrush = "TextFillColorPrimaryBrush";
}
