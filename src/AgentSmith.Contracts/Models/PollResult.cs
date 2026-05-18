namespace AgentSmith.Contracts.Models;

/// <summary>
/// p0140c: per-cycle counts returned by IEventPoller.PollAsync. Pollers no longer yield
/// ClaimRequests — they call SpawnPipelineRunsUseCase directly per matched project and
/// report this summary so PollerHostedService can log observability metrics.
/// </summary>
public sealed record PollResult(
    int PolledTickets,
    int MatchedProjects,
    int Spawned,
    int StatusFiltered,
    int ZeroMatched)
{
    public static PollResult Empty() => new(0, 0, 0, 0, 0);
}
