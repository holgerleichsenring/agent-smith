namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-23-933db: whether a claim won the PoC's run slot; a lost one names the run holding it.</summary>
public sealed record PocClaim(bool Won, string? LiveRunId)
{
    public static PocClaim Taken { get; } = new(true, null);

    public static PocClaim HeldBy(string? runId) => new(false, runId);
}
