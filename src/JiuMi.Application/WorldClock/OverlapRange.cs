using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// A contiguous run of hours that share the maximum overlap.
/// </summary>
/// <param name="StartHour">The first hour of the run, 0 to 23.</param>
/// <param name="EndHour">The hour after the run, 1 to 24, so an evening run ends at 24.</param>
public sealed record OverlapRange(int StartHour, int EndHour);
