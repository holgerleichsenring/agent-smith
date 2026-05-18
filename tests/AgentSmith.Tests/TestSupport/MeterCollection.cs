namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// p0140e: test classes that subscribe to the static <see cref="System.Diagnostics.Metrics.Meter"/>
/// "AgentSmith" share this collection so xUnit serializes their execution. Without it,
/// parallel runs leak counter measurements across tests that subscribe to the same
/// instrument — every active <c>MeterListener</c> sees every <c>Counter.Add</c>, regardless
/// of which test triggered it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MeterCollection
{
    public const string Name = "AgentSmithMeter";
}
