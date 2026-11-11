namespace JiuMi.Application.Pet;

/// <summary>
/// A resolved pet position together with the screen and pixel size it belongs to.
/// </summary>
/// <param name="Position">The clamped top-left position in physical pixels.</param>
/// <param name="WorkArea">The screen work area that received the pet.</param>
/// <param name="PixelSize">The pet window size in physical pixels on that screen.</param>
public readonly record struct PetPlacementResult(
    PetPoint Position,
    PetWorkArea WorkArea,
    int PixelSize);
