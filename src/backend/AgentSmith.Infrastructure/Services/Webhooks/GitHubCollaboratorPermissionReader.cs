using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>2026-10-25-9dbbe: Octokit's permission review on the repository's own API base.</summary>
public sealed class GitHubCollaboratorPermissionReader(
    ICredentialResolver credentials, IGitHubClientFactory clients) : IGitHubCollaboratorPermissionReader
{
    public async Task<IReadOnlyList<string>> ReadAsync(
        RepoConnection repo, string login, CancellationToken cancellationToken)
    {
        var address = GitHubRepoAddress.Parse(repo.Url!);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var answer = await clients.Create(address, credentials.For(repo))
                .Repository.Collaborator.ReviewPermission(address.Owner, address.Repo, login);
            return [.. new[] { answer.RoleName, answer.Permission }.OfType<string>()];
        }
        catch (NotFoundException)
        {
            return [];
        }
    }
}
