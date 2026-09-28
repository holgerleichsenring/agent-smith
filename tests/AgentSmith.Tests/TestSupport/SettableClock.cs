namespace AgentSmith.Tests.TestSupport;

/// <summary>A clock a test moves by hand; it starts at the real time.</summary>
internal sealed class SettableClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => Now;
}
