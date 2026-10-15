namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>2026-10-13-9a59b: a merge request as GitLab's merge-request read returns it.</summary>
/// <param name="State">GitLab's own word: opened, merged, closed or locked.</param>
/// <param name="Sha">The merge request's head commit.</param>
public sealed record GitLabMergeRequest(
    long? AuthorId, string State, DateTimeOffset? CreatedAt, DateTimeOffset? MergedAt, DateTimeOffset? ClosedAt, string? Sha = null);
