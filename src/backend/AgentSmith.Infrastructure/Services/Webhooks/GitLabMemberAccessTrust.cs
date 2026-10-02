using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// On GitLab, write access is the Developer role (access level 30) or above on the
/// project, directly or through a group. A repository no project declares, or a lookup
/// that fails, is untrusted.
/// </summary>
public sealed class GitLabMemberAccessTrust(
    IPrCommentRepoLookup repos,
    IGitLabMemberAccessReader members,
    IPrCommentTrustVerdictCache verdicts,
    ILogger<GitLabMemberAccessTrust> logger) : IPrCommentAuthorTrust
{
    private const int DeveloperAccessLevel = 30;

    public async Task<bool> IsTrustedAsync(PrCommentAuthor author, CancellationToken cancellationToken)
    {
        try
        {
            var configured = repos.Find(author.RepositoryUrl);
            if (configured?.Repo is not { Url: { } repositoryUrl } repo)
            {
                logger.LogInformation("GitLab repository {Repo} is not configured; its comments are untrusted",
                    author.RepositoryUrl);
                return false;
            }

            return await verdicts.GetOrLookupAsync(
                // 2026-10-02-5f89a: a project id is unique per instance only, so the host is in the key.
                $"gitlab:{new Uri(repositoryUrl).Authority}:{author.RepositoryId}:{author.AuthorId}",
                async () => await members.ReadAccessLevelAsync(
                    repo, author.RepositoryId, author.AuthorId, cancellationToken) >= DeveloperAccessLevel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GitLab member lookup for {Author} on {Repo} failed; treating the author as untrusted",
                author.AuthorLogin, author.RepositoryUrl);
            return false;
        }
    }
}
