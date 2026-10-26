using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// On Azure DevOps, write access is the effective Git Repositories "Contribute" permission
/// on the repository, however it is granted. A repository no project declares, or a lookup
/// that fails, is untrusted.
/// </summary>
public sealed class AzureDevOpsRepoContributeTrust(
    IPrCommentRepoLookup repos,
    IAzureDevOpsPermissionReader permissions,
    IPrCommentTrustVerdictCache verdicts,
    ILogger<AzureDevOpsRepoContributeTrust> logger) : IPrCommentAuthorTrust
{
    private static readonly Guid GitRepositoriesNamespace = new("2e9eb7ed-3c0a-47d4-87c1-0ffdd275fd87");
    private const int ContributeBit = 4;

    public async Task<bool> IsTrustedAsync(PrCommentAuthor author, CancellationToken cancellationToken)
    {
        try
        {
            var configured = repos.Find(author.RepositoryUrl);
            if (configured?.Repo is not { Url: { } repositoryUrl } repo)
            {
                logger.LogInformation("Azure DevOps repository {Repo} is not configured; its comments are untrusted",
                    author.RepositoryUrl);
                return false;
            }

            return await verdicts.GetOrLookupAsync(
                // 2026-10-02-5f89a: keyed by organization too, so two organizations never share a verdict.
                $"azuredevops:{AzureDevOpsRepoAddress.Parse(repositoryUrl).OrganizationUrl}:{author.RepositoryId}:{author.AuthorId}",
                () => HasContributeAsync(repo, repositoryUrl, author, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure DevOps permission lookup for {Author} on {Repo} failed; treating the author as untrusted",
                author.AuthorLogin, author.RepositoryUrl);
            return false;
        }
    }

    private async Task<bool> HasContributeAsync(
        RepoConnection repo, string repositoryUrl, PrCommentAuthor author, CancellationToken cancellationToken)
    {
        var projectId = Guid.Parse(author.ProjectId ?? "");
        var repositoryId = Guid.Parse(author.RepositoryId);
        string[] tokens = [$"repoV2/{projectId}/{repositoryId}", $"repoV2/{projectId}", "repoV2"];
        var effective = await permissions.ReadAsync(
            repo, AzureDevOpsRepoAddress.Parse(repositoryUrl).OrganizationUrl, GitRepositoriesNamespace, tokens,
            Guid.Parse(author.AuthorId), cancellationToken);
        return effective?.Grants(ContributeBit) == true;
    }
}
