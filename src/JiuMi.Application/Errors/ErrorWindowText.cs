namespace JiuMi.Application.Errors;

/// <summary>
/// The zh-CN text of the error window, embedded rather than localized: the window can appear before
/// localization is ready or when its files are damaged, and its text must not change with the language.
/// The copied details are the exception; they match what the info bar copies (see
/// <see cref="ErrorWindowRequest.DetailsText"/>).
/// </summary>
public static class ErrorWindowText
{
    /// <summary>The window title, which is the product name.</summary>
    public const string AppTitle = "JiuMi";

    /// <summary>The copy-details button label.</summary>
    public const string CopyDetails = "复制详情";

    /// <summary>The copy-details label shown for two seconds after a successful copy.</summary>
    public const string Copied = "已复制";

    /// <summary>The expander header that reveals the details box.</summary>
    public const string DetailsHeader = "查看详情";

    /// <summary>The exit button label.</summary>
    public const string Exit = "退出";

    /// <summary>The continue-running button label, offered only when the main window is usable.</summary>
    public const string Continue = "继续运行";

    private static readonly ErrorWindowMessage _default = new(
        "发生了未预期的错误",
        "应用遇到了未预期的问题。可以复制详情反馈给开发者，然后退出。");

    private static readonly ErrorWindowMessage _uiThreadExceptionBlocking = new(
        "发生未处理的错误",
        "界面线程上出现了未处理的错误，应用无法继续。可以复制详情反馈给开发者，然后退出。");

    private static readonly ErrorWindowMessage _uiThreadExceptionContinuing = new(
        "发生未处理的错误",
        "界面线程上出现了未处理的错误，但应用仍可继续使用。可以复制详情反馈给开发者，或继续运行。");

    /// <summary>
    /// Gets the headline and explanation for a failure.
    /// </summary>
    /// <param name="code">The stable event code.</param>
    /// <param name="canContinue">Whether the window offers continuing after the failure.</param>
    /// <returns>The embedded text, or the generic text when the code has no specific wording.</returns>
    public static ErrorWindowMessage MessageFor(string code, bool canContinue) => code switch
    {
        ErrorCodes.UnexpectedUiThreadException when canContinue => _uiThreadExceptionContinuing,
        ErrorCodes.UnexpectedUiThreadException => _uiThreadExceptionBlocking,
        _ => _default,
    };
}
