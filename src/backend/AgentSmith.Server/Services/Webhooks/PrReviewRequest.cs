using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: a Request changes as a code host's webhook reports it — the repository, the
/// pull request's head branch and whether it lives in the same repository, the reviewer, the pull
/// request's author and its URL. 2026-10-08-0781: no act — the worker reads it from the host.
/// PrAuthorId is null for an Azure DevOps vote: a -5 counts whoever cast it, since we never vote -5.
/// </summary>
public sealed record PrReviewRequest(
    RepoType Host,
    string RepoUrl,
    string? HeadRef,
    bool SameRepository,
    PrCommentAuthor Reviewer,
    string? PrAuthorId,
    string PrUrl);
