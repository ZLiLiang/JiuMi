using JiuMi.Infrastructure.WeChatVideo;

namespace JiuMi.Infrastructure.Tests;

internal sealed class InMemoryWinInetRegistry : IWinInetRegistry
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    public int WriteCount
    {
        get; private set;
    }

    public IReadOnlyDictionary<string, object> Values => _values;

    public void Set(string name, object? value)
    {
        if (value is null)
        {
            _values.Remove(name);
        }
        else
        {
            _values[name] = value;
        }
    }

    public int? ReadInt32(string name) => _values.TryGetValue(name, out object? value) ? value as int? : null;

    public string? ReadString(string name) => _values.TryGetValue(name, out object? value) ? value as string : null;

    public void WriteInt32(string name, int value)
    {
        WriteCount++;
        _values[name] = value;
    }

    public void WriteString(string name, string value)
    {
        WriteCount++;
        _values[name] = value;
    }

    public void Delete(string name)
    {
        WriteCount++;
        _values.Remove(name);
    }
}
