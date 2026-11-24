namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: a clock the lifetime tests set and move.</summary>
internal sealed class PocClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
