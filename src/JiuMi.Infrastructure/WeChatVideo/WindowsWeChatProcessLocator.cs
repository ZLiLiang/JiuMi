using System.Diagnostics;
using System.Runtime.Versioning;

namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// Locates running <c>Weixin</c> and <c>WeChatAppEx</c> processes on Windows and identifies
/// the <c>network.mojom.NetworkService</c> subprocess by inspecting its command line.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsWeChatProcessLocator : IWeChatProcessLocator
{
    private const string WeixinProcessName = "Weixin";
    private const string WeChatAppExProcessName = "WeChatAppEx";
    private const string NetworkServiceMarker = "network.mojom.NetworkService";

    private static readonly string[] _ignoredSubprocessMarkers =
    [
        "--type=wxocr",
        "--type=wxutility",
        "--type=wxpublic",
        "--type=xplayer",
        "--type=gpu-process",
        "--type=crashpad-handler",
        "ILinkServiceHost",
    ];

    /// <inheritdoc />
    public IReadOnlyList<WeChatProcessInfo> FindTargetProcesses()
    {
        var results = new List<WeChatProcessInfo>();
        CollectProcesses(WeixinProcessName, results);
        CollectProcesses(WeChatAppExProcessName, results);
        return results;
    }

    private static void CollectProcesses(
        string processName,
        List<WeChatProcessInfo> results)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        foreach (Process process in processes)
        {
            using (process)
            {
                int pid = process.Id;
                bool hasCommandLine = WindowsWeChatProcessMemoryReader.TryReadCommandLine(pid, out string? commandLine) &&
                    commandLine is not null;

                if (hasCommandLine && IsIgnoredSubprocess(commandLine!))
                {
                    continue;
                }

                bool isNetworkService = hasCommandLine &&
                    commandLine!.Contains(NetworkServiceMarker, StringComparison.OrdinalIgnoreCase);

                results.Add(new WeChatProcessInfo(pid, processName, isNetworkService));
            }
        }
    }

    private static bool IsIgnoredSubprocess(string commandLine)
    {
        foreach (string marker in _ignoredSubprocessMarkers)
        {
            if (commandLine.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
