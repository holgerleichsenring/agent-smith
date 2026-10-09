using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Application.Services.Rework;

/// <summary>2026-10-08-e8b9d: the default where no per-host trust is registered — nobody's review
/// note counts, because a stranger's comment must never become an instruction.</summary>
public sealed class UntrustingReviewAuthorTrust : IPrReviewAuthorTrust
{
    public Task<bool> IsTrustedAsync(RepoType host, PrCommentAuthor author, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
