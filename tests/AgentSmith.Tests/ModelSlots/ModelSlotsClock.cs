namespace AgentSmith.Tests.ModelSlots;

/// <summary>2026-11-20-24bd: a clock a test moves by hand.</summary>
internal sealed class ModelSlotsClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
