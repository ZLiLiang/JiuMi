namespace JiuMi.Infrastructure.Tests;

// LoggingPipeline owns the process-wide Serilog SelfLog switch; tests that create or dispose a
// pipeline must not run alongside each other.
[CollectionDefinition("Logging", DisableParallelization = true)]
public sealed class LoggingCollectionDefinition
{
}
