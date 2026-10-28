using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// One hour column of the plan grid, in reference-zone time.
/// </summary>
/// <param name="Instant">The absolute instant at the start of the column.</param>
/// <param name="WorkingCount">How many available places are working at that instant.</param>
public sealed record WorldClockColumn(DateTimeOffset Instant, int WorkingCount);
