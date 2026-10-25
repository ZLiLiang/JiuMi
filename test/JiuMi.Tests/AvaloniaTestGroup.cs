namespace JiuMi.Tests;

/// <summary>
/// Serializes the tests that share one Avalonia application.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AvaloniaTestGroup
{
    /// <summary>The collection name used by the headless tests.</summary>
    public const string Name = "Avalonia";
}
