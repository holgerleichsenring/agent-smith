using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9d: a review note's author judged by the same per-host trust a PR comment command
/// is — write access to the repository. A host without one trusts nobody.
/// </summary>
public sealed class PrReviewAuthorTrust(
    [FromKeyedServices("github")] IPrCommentAuthorTrust github,
    [FromKeyedServices("gitlab")] IPrCommentAuthorTrust gitlab,
    [FromKeyedServices("azuredevops")] IPrCommentAuthorTrust azureDevOps) : IPrReviewAuthorTrust
{
    public Task<bool> IsTrustedAsync(RepoType host, PrCommentAuthor author, CancellationToken cancellationToken) => host switch
    {
        RepoType.GitHub => github.IsTrustedAsync(author, cancellationToken),
        RepoType.GitLab => gitlab.IsTrustedAsync(author, cancellationToken),
        RepoType.AzureDevOps => azureDevOps.IsTrustedAsync(author, cancellationToken),
        _ => Task.FromResult(false),
    };
}
