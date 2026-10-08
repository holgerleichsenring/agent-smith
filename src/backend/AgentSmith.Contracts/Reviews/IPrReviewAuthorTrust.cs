using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-08-e8b9d: whether a review note's author may write to the repository — the same verdict a
/// PR comment command gets, chosen by the repository's host. A stranger's comment on a public
/// repository is not reviewer feedback.
/// </summary>
public interface IPrReviewAuthorTrust
{
    Task<bool> IsTrustedAsync(RepoType host, PrCommentAuthor author, CancellationToken cancellationToken);
}
