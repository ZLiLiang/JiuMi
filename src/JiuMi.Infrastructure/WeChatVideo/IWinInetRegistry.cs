namespace JiuMi.Infrastructure.WeChatVideo;

/// <summary>
/// The five HKCU WinINet values the capture touches.
/// </summary>
internal interface IWinInetRegistry
{
    int? ReadInt32(string name);

    string? ReadString(string name);

    void WriteInt32(string name, int value);

    void WriteString(string name, string value);

    void Delete(string name);
}
