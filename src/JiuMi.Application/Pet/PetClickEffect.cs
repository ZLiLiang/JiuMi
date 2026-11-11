namespace JiuMi.Application.Pet;

/// <summary>
/// What a click should show on top of the animation.
/// </summary>
public enum PetClickEffect
{
    /// <summary>
    /// The click did not change the animation.
    /// </summary>
    None,

    /// <summary>
    /// The pet reacted; the heart overlay belongs to this click.
    /// </summary>
    React,

    /// <summary>
    /// The pet was asleep and started to wake; no heart is shown.
    /// </summary>
    Wake,
}
