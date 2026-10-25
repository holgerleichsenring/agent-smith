using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// 2026-10-25-9dbbe: a user's permission on a GitHub repository
/// (<c>GET /repos/{owner}/{repo}/collaborators/{login}/permission</c>), asked of the configured
/// repository's own host with its auth secret: the role name (admin, maintain, write, triage, read
/// or a custom role) and the legacy permission. Empty when GitHub does not know the user there.
/// </summary>
public interface IGitHubCollaboratorPermissionReader
{
    Task<IReadOnlyList<string>> ReadAsync(RepoConnection repo, string login, CancellationToken cancellationToken);
}
