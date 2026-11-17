namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-17-7c9bc: what a resumed run's earlier segments spent, carried into this segment's ledger as one
/// figure — their calls were recorded by the process that ran them, so only their totals cross the park.
/// </summary>
public sealed record RecordedCallCarry(
    long TokensIn, long TokensOut, long CachedTokensIn, long CacheCreationTokensIn, decimal CostUsd)
{
    public static readonly RecordedCallCarry None = new(0, 0, 0, 0, 0m);

    public bool IsEmpty => this == None;
}
