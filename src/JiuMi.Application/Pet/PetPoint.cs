namespace JiuMi.Application.Pet;

/// <summary>
/// A point in physical screen pixels.
/// </summary>
/// <param name="X">The horizontal pixel coordinate.</param>
/// <param name="Y">The vertical pixel coordinate.</param>
public readonly record struct PetPoint(int X, int Y);
