using System.ComponentModel;
using System.Diagnostics;
using JiuMi.Application;

namespace JiuMi.Infrastructure;

/// <summary>
/// Opens folders and links through the operating system's shell.
/// </summary>
public sealed class ShellLauncher : ISystemLauncher
{
    /// <inheritdoc />
    public void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    /// <inheritdoc />
    public void OpenUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
    }

    private static void Start(ProcessStartInfo startInfo)
    {
        try
        {
            using Process? process = Process.Start(startInfo);
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("The system could not open the requested target.", exception);
        }
    }
}
