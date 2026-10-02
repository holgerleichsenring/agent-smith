using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Reads a user's access level on a GitLab project, inherited group membership included
/// (<c>GET /projects/:id/members/all/:user_id</c>). <c>null</c> when the user is no member.
/// 2026-10-02-5f89a: asked of the configured repository, whose host and auth secret it uses.
/// </summary>
public interface IGitLabMemberAccessReader
{
    Task<int?> ReadAccessLevelAsync(
        RepoConnection repo, string projectId, string userId, CancellationToken cancellationToken);
}
