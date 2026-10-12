namespace AgentSmith.Contracts.Sweep;

/// <summary>2026-10-08-10b0: an open pull request as the host's list shows it — the facts a review
/// run starts from, its labels, and when it was opened and last changed.</summary>
public sealed record OpenPullRequest(
    string Number, string Url, string? HeadSha, string? BaseSha, string? HeadBranch, string? Author,
    IReadOnlyList<string> Labels, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    /// <summary>True when the head branch lives in this repository, not a fork — a comment command on
    /// a ticket branch is a rework act only then.</summary>
    public bool SameRepository { get; init; }
}
