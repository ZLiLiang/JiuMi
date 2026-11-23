namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Result of attempting to open or read a target WeChat process with minimal read-only rights.
/// </summary>
internal enum WeChatProcessAccessStatus
{
    /// <summary>The process was opened and read successfully.</summary>
    Accessible = 0,

    /// <summary>The process exited or could not be found.</summary>
    NotFound = 1,

    /// <summary>Opening the process failed with access denied (for example, privilege mismatch).</summary>
    AccessDenied = 2,

    /// <summary>Opening or reading the process failed for another operating system reason.</summary>
    Failed = 3,
}
