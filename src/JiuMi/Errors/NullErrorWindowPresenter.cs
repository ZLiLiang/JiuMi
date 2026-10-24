namespace JiuMi.Errors;

/// <summary>
/// Stands in for <see cref="IErrorWindowPresenter"/> when the shell is constructed without one,
/// such as by the XAML designer.
/// </summary>
internal sealed class NullErrorWindowPresenter : IErrorWindowPresenter
{
    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static NullErrorWindowPresenter Instance
    {
        get;
    } = new();

    private NullErrorWindowPresenter()
    {
    }

    /// <inheritdoc />
    public void PresentUnhandledException(Exception exception) => ArgumentNullException.ThrowIfNull(exception);
}
