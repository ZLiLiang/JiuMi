namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Carries the one-shot residual proxy result from the startup sequence to the shell.
/// </summary>
public sealed class WeChatVideoStartupNotice
{
    private readonly object _gate = new();
    private WeChatVideoResidualRestoreResult? _residual;

    /// <summary>
    /// Stores the result the startup check produced. The first result wins.
    /// </summary>
    /// <param name="result">The residual check result.</param>
    public void Report(WeChatVideoResidualRestoreResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_gate)
        {
            _residual ??= result;
        }
    }

    /// <summary>
    /// Takes the stored result so it is shown only once.
    /// </summary>
    /// <returns>The result, or <see langword="null"/> when nothing was reported or it was taken.</returns>
    public WeChatVideoResidualRestoreResult? Take()
    {
        lock (_gate)
        {
            WeChatVideoResidualRestoreResult? result = _residual;
            _residual = null;
            return result;
        }
    }
}
