namespace JiuMi.ViewModels;

/// <summary>
/// The state of one file in the conversion queue.
/// </summary>
public enum ImageConvertRowStatus
{
    /// <summary>Queued and ready to convert.</summary>
    Pending,

    /// <summary>Being decoded and encoded.</summary>
    Converting,

    /// <summary>Written to the output path.</summary>
    Done,

    /// <summary>Failed; the reason is shown below the file name.</summary>
    Failed,
}
