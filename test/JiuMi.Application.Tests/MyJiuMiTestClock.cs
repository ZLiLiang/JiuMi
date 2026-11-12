using JiuMi.Application.MyJiuMi;

namespace JiuMi.Application.Tests;

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public ManualTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration) => _now += duration;
}

internal sealed class FixedRandom : Random
{
    private readonly double _value;

    public FixedRandom(double value) => _value = value;

    public override double NextDouble() => _value;
}

internal static class MyJiuMiTestTimeline
{
    public static void Advance(
        MyJiuMiStateMachine machine,
        ManualTimeProvider time,
        TimeSpan duration,
        TimeSpan? step = null)
    {
        TimeSpan interval = step ?? TimeSpan.FromMilliseconds(50);
        TimeSpan remaining = duration;
        while (remaining > TimeSpan.Zero)
        {
            TimeSpan delta = remaining < interval ? remaining : interval;
            time.Advance(delta);
            machine.Update();
            remaining -= delta;
        }
    }
}
