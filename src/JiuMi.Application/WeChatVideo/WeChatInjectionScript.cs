using System.Reflection;

namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Loads the embedded page script and points it at this session's loopback listener.
/// </summary>
public static class WeChatInjectionScript
{
    /// <summary>
    /// The placeholder replaced by the receiver URL.
    /// </summary>
    public const string ReceiverPlaceholder = "__JIUMI_CAPTURE_URL__";

    private const string ResourceName = "JiuMi.Application.WeChatVideo.InjectionScript.js";
    private static readonly Lazy<string> _template = new(LoadTemplate, isThreadSafe: true);

    /// <summary>
    /// Builds the script for one session.
    /// </summary>
    /// <param name="receiverUrl">The loopback URL that accepts the captured items.</param>
    /// <returns>The script with the receiver URL substituted.</returns>
    public static string Create(Uri receiverUrl)
    {
        ArgumentNullException.ThrowIfNull(receiverUrl);

        if (!receiverUrl.IsAbsoluteUri ||
            receiverUrl.Scheme != Uri.UriSchemeHttp ||
            !receiverUrl.IsLoopback ||
            receiverUrl.Port <= 0 ||
            receiverUrl.AbsolutePath.Length <= 1)
        {
            throw new ArgumentException("The receiver URL must be an absolute loopback HTTP URL with a path.", nameof(receiverUrl));
        }

        string script = _template.Value.Replace(
            ReceiverPlaceholder,
            receiverUrl.AbsoluteUri,
            StringComparison.Ordinal);
        if (script.Contains(ReceiverPlaceholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The embedded capture script still contains the receiver placeholder.");
        }

        return script;
    }

    private static string LoadTemplate()
    {
        using Stream? stream = typeof(WeChatInjectionScript).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"The embedded capture script '{ResourceName}' is missing.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
