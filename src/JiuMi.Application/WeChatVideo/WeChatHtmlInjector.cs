namespace JiuMi.Application.WeChatVideo;

/// <summary>
/// Inserts the capture script into the WeChat page exactly once.
/// </summary>
public static class WeChatHtmlInjector
{
    /// <summary>
    /// The marker that makes a repeated injection recognisable.
    /// </summary>
    public const string Marker = "__JIUMI_WECHAT_CAPTURE__";

    /// <summary>
    /// Inserts the script before the closing body tag, or appends it when the page has none.
    /// </summary>
    /// <param name="html">The response body.</param>
    /// <param name="script">The script without a surrounding script element.</param>
    /// <returns>The modified page, or the original page when the marker is already present.</returns>
    public static string Inject(string html, string script)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(script);

        if (html.Contains(Marker, StringComparison.Ordinal))
        {
            return html;
        }

        string element = "<script>/*" + Marker + "*/\n" + script + "\n</script>";
        int bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd < 0 ? html + element : html.Insert(bodyEnd, element);
    }
}
