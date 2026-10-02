namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5ab2b: decides, at the moment a consumer pops a request off the Redis queue,
/// whether this copy of it may start. A request whose run row holds the stored request is
/// claimed by one conditional update, so of two copies — the original and one the sweeper
/// pushed again after a flush, or a duplicate pop — exactly one starts.
/// </summary>
public interface IRunStartClaim
{
    Task<RunStartClaimOutcome> ClaimAsync(string runId, CancellationToken cancellationToken);
}
