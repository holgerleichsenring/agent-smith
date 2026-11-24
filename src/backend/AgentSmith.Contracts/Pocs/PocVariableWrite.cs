namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dg: the seed of a poc-variables run — the PoC, the entries to write and the person the endpoint let
/// write them. In memory only (the run goes through IActRun, never the queue) and excluded from checkpoints, because a
/// secret value travels in it. Says only the PoC and the names.
/// </summary>
public sealed record PocVariableWrite(long PocId, IReadOnlyList<PocVariableEntry> Entries, string Person)
{
    public override string ToString() => $"PoC {PocId}: {string.Join(", ", Entries)} by {Person}";
}
