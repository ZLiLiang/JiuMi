using System;

namespace JiuMi.Application.WorldClock;

/// <summary>
/// The local time and status of a place at one absolute instant.
/// </summary>
/// <param name="LocalDateTime">The wall-clock time in the place's zone; default when the zone is unavailable.</param>
/// <param name="Status">The status of the place at that instant.</param>
public sealed record WorldClockLocalTime(DateTime LocalDateTime, WorldClockStatus Status);
