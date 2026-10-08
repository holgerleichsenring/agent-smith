using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Webhooks;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: a Request changes as a code host's webhook reports it — the repository, the
/// pull request's head branch and whether it lives in the same repository, the reviewer, the pull
/// request's author, its number, and the act with the host's time.
/// </summary>
public sealed record PrReviewRequest(
    RepoType Host,
    string RepoUrl,
    string? HeadRef,
    bool SameRepository,
    PrCommentAuthor Reviewer,
    string? PrAuthorId,
    string PrNumber,
    ReworkAct Act);
