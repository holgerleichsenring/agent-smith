using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// 2026-10-25-9dbbe: on GitHub, write access is the write, maintain or admin role on the repository,
/// asked of GitHub — not the payload's author_association, which says MEMBER for any organisation
/// member and COLLABORATOR for a read-only one. A repository no project declares, a user GitHub does
/// not know there, or a lookup that fails, is untrusted.
/// </summary>
public sealed class GitHubCollaboratorPermissionTrust(
    IPrCommentRepoLookup repos,
    IGitHubCollaboratorPermissionReader permissions,
    IPrCommentTrustVerdictCache verdicts,
    ILogger<GitHubCollaboratorPermissionTrust> logger) : IPrCommentAuthorTrust
{
    private static readonly HashSet<string> WriteRoles = new(StringComparer.OrdinalIgnoreCase) { "admin", "maintain", "write" };

    public async Task<bool> IsTrustedAsync(PrCommentAuthor author, CancellationToken cancellationToken)
    {
        try
        {
            var configured = repos.Find(author.RepositoryUrl);
            if (configured?.Repo is not { Url: { } repositoryUrl } repo)
            {
                logger.LogInformation("GitHub repository {Repo} is not configured; its comments are untrusted",
                    author.RepositoryUrl);
                return false;
            }

            return await verdicts.GetOrLookupAsync(
                $"github:{new Uri(repositoryUrl).Authority}:{author.RepositoryId}:{author.AuthorId}",
                async () => (await permissions.ReadAsync(repo, author.AuthorId, cancellationToken)).Any(WriteRoles.Contains));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GitHub permission lookup for {Author} on {Repo} failed; treating the author as untrusted",
                author.AuthorLogin, author.RepositoryUrl);
            return false;
        }
    }
}
