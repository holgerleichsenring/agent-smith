namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-13-9a59b: a pull request's state on its host — open, merged or closed — and the
/// decisions that stand on it now. The author, the token's identity and bots are never among the
/// decisions; on Azure DevOps a -5 counts whoever cast it, since agent-smith only votes +10.
/// Implemented by the source providers, reached by casting the repo's provider.
/// </summary>
public interface IPrReviewStateReader
{
    Task<PrReviewState> ReadAsync(string prUrl, CancellationToken cancellationToken);
}
