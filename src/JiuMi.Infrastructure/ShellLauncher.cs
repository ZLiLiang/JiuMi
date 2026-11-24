using System.ComponentModel;
using System.Diagnostics;
using JiuMi.Application;

namespace JiuMi.Infrastructure;

/// <summary>
/// Opens files, folders and links through the operating system's shell.
/// </summary>
public sealed class ShellLauncher : ISystemLauncher
{
    private readonly Action<ProcessStartInfo> _start;
    private readonly Func<bool> _isWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellLauncher"/> class.
    /// </summary>
    public ShellLauncher()
        : this(Start, OperatingSystem.IsWindows)
    {
    }

    internal ShellLauncher(Action<ProcessStartInfo> start, Func<bool> isWindows)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(isWindows);
        _start = start;
        _isWindows = isWindows;
    }

    /// <inheritdoc />
    public void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    /// <inheritdoc />
    public void OpenUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        _start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
    }

    /// <inheritdoc />
    public void OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    /// <inheritdoc />
    public void RevealInFolder(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string trimmed = filePath.Trim();
        if (trimmed.Contains('"', StringComparison.Ordinal) ||
            trimmed.AsSpan().IndexOfAny('\0', '\r', '\n') >= 0)
        {
            throw new ArgumentException("The file path contains invalid characters.", nameof(filePath));
        }

        string fullPath = Path.GetFullPath(trimmed);
        if (_isWindows())
        {
            // explorer.exe parses /select,"<path>" as a single non-standard argument rather than
            // standard CRT argv tokens, so ArgumentList escapes the embedded quotes and breaks selection.
            // Quotes and control characters are rejected above, and UseShellExecute is false.
            _start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{fullPath}\"",
                UseShellExecute = false,
            });
            return;
        }

        string? parentDirectory = Path.GetDirectoryName(fullPath);
        OpenFolder(string.IsNullOrWhiteSpace(parentDirectory) ? fullPath : parentDirectory);
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
