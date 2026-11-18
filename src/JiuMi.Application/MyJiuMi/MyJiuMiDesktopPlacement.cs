namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// The resolved desktop window position, its screen and its physical size.
/// </summary>
/// <param name="Origin">The window's top-left position in physical pixels.</param>
/// <param name="Area">The work area that received the window.</param>
/// <param name="PixelSize">The window's width and height in physical pixels.</param>
public readonly record struct MyJiuMiDesktopPlacement(
    MyJiuMiScreenPoint Origin,
    MyJiuMiScreenArea Area,
    int PixelSize);
