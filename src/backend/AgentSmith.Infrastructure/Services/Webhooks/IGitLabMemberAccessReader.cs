namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Reads a user's access level on a GitLab project, inherited group membership included
/// (<c>GET /projects/:id/members/all/:user_id</c>). <c>null</c> when the user is no member.
/// </summary>
public interface IGitLabMemberAccessReader
{
    Task<int?> ReadAccessLevelAsync(
        string repositoryUrl, string projectId, string userId, CancellationToken cancellationToken);
}
