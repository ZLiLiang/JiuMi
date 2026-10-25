using FluentAvalonia.Styling;

namespace JiuMi.Tests;

/// <summary>
/// Minimal application whose styles match the shell, so resource lookups behave as they do at runtime.
/// </summary>
public sealed class TestApplication : global::Avalonia.Application
{
    /// <inheritdoc />
    public override void Initialize() => Styles.Add(new FluentAvaloniaTheme());
}
