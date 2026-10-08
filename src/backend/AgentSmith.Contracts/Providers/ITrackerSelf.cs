namespace AgentSmith.Contracts.Providers;

/// <summary>2026-10-08-2123: who the tracker's token is — Jira /myself, GitHub and GitLab GET /user,
/// Azure DevOps connectionData. Null when the tracker cannot say.</summary>
public interface ITrackerSelf
{
    Task<TrackerActor?> SelfAsync(CancellationToken cancellationToken);
}
