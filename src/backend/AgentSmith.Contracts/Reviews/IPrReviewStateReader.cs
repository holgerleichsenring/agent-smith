namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-13-9a59b: a pull request's state on its host — open, merged or closed — and the
/// decisions that stand on it now. The pull request's author, the token's own identity and bots
/// are never among the decisions, so our own auto-complete vote is never an approval.
/// Implemented by the source providers, reached by casting the repo's provider.
/// </summary>
public interface IPrReviewStateReader
{
    Task<PrReviewState> ReadAsync(string prUrl, CancellationToken cancellationToken);
}
