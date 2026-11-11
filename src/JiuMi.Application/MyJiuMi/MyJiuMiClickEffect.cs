namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// What a click on the cat did.
/// </summary>
public enum MyJiuMiClickEffect
{
    /// <summary>
    /// The click was ignored because the page is inactive or the cat is being dragged.
    /// </summary>
    None,

    /// <summary>
    /// The cat reacted and the heart overlay follows.
    /// </summary>
    React,

    /// <summary>
    /// The cat was asleep and started to wake up; no heart appears.
    /// </summary>
    Wake,
}
