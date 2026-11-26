namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// An explicit anchor point in canvas pixel coordinates that aligns the cat to the floor line and
/// horizontal drop point.
/// </summary>
/// <param name="X">The horizontal anchor in pixels from the left edge of the canvas.</param>
/// <param name="Y">The vertical anchor in pixels from the top edge of the canvas.</param>
public readonly record struct MyJiuMiPetPackAnchor(double X, double Y);
