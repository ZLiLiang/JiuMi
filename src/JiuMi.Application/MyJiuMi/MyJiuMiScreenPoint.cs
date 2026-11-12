namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// A point in physical screen pixels.
/// </summary>
/// <param name="X">The horizontal pixel coordinate.</param>
/// <param name="Y">The vertical pixel coordinate.</param>
public readonly record struct MyJiuMiScreenPoint(int X, int Y);
