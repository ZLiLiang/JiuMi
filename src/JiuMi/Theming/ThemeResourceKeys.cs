namespace JiuMi.Theming;

/// <summary>
/// Resource keys shared by the theme applier and the window backdrop.
/// </summary>
internal static class ThemeResourceKeys
{
    /// <summary>The Fluent palette's solid surface brush, also used when the backdrop gradient is missing.</summary>
    public const string SolidBackgroundBrush = "SolidBackgroundFillColorBaseBrush";

    /// <summary>The gradient backdrop that stands in for Mica when it is unavailable.</summary>
    public const string WindowBackdropBrush = "JiuMiWindowBackdropBrush";

    /// <summary>The primary text brush, paired with the surface brush to detect a full palette.</summary>
    public const string PrimaryTextBrush = "TextFillColorPrimaryBrush";
}
