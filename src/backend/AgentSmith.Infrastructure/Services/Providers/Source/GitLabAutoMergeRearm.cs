using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-7a92c: arms a GitLab merge request's auto-merge again after GitLab cancelled it — GitLab cancels it when new commits
/// arrive, a refresh's merge of the base among them — through the same request the first arm made (<see cref="GitLabMergeModes"/>),
/// so GitLab's merge checks still decide. One armed already is left; one from another project is refused, as GitLab's own cancel
/// for a push it did not review is not agent-smith's to overrule.
/// </summary>
public sealed class GitLabAutoMergeRearm(GitLabMergeRequestRead mergeRequests, GitLabMergeModes merge)
{
    public async Task<PullRequestCompletion> RearmAsync(string prUrl, CancellationToken ct)
    {
        if (!GitLabMergeRequestUpdater.TryParseMergeRequestIid(prUrl, out var iid))
            return PullRequestCompletion.Refused($"'{prUrl}' is not a GitLab merge request URL.");
        var mr = await mergeRequests.ReadAsync(iid, ct);
        if (mr.State != "opened") return PullRequestCompletion.Refused($"GitLab says !{iid} is {mr.State}.");
        if (mr.AutoMergeArmed == true) return PullRequestCompletion.Armed("GitLab's auto-merge is set already.");
        if (mr.SameProject != true)
            return PullRequestCompletion.Refused($"!{iid} comes from another project; its auto-merge is not armed again by agent-smith.");
        return await merge.ArmAsync(prUrl, ct);
    }
}
