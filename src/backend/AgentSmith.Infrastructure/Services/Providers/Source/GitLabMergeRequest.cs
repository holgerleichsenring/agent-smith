namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>2026-10-13-9a59b: a merge request as GitLab's merge-request read returns it.</summary>
/// <param name="State">GitLab's own word: opened, merged, closed or locked.</param>
/// <param name="Sha">The merge request's head commit.</param>
/// <param name="IsDraft">2026-11-23-8d16: GitLab's draft flag.</param>
/// <param name="MergeCommitSha">2026-11-23-8df6a: the merge commit; null for a squash or fast-forward merge and before one.</param>
/// <param name="SquashCommitSha">2026-11-23-8df6a: the squash commit of a squash merge.</param>
public sealed record GitLabMergeRequest(
    long? AuthorId, string State, DateTimeOffset? CreatedAt, DateTimeOffset? MergedAt, DateTimeOffset? ClosedAt, string? Sha = null,
    bool IsDraft = false, string? MergeCommitSha = null, string? SquashCommitSha = null);
