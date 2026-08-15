namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0413: the empty <see cref="RunSnapshot"/> a live fold starts from. Split out
/// of the record so the contract file states the contract and this states what a
/// run looks like before any event has landed on it.
/// </summary>
internal static class RunSnapshotSeed
{
    public static RunSnapshot Empty(string runId) => new(
        runId, "unknown", "unknown", Array.Empty<string>(),
        "running", null, null,
        DateTimeOffset.UtcNow, null, 0, 0, null, 0, null,
        CostUsd: 0m, LlmCalls: 0);
}
