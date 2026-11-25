namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-25-96aeb: how a merge-green run was asked, from its <see cref="Contracts.Runs.CiGreenMergeRequest"/>: by somebody
/// (a refusal is marked on its own run only), judged (no prerequisite, rung or unreadable-base refusal holds it), checking its
/// target (the series master's: a base off the series and default branch is refused even when judged), the override reason
/// a judged master merge gave, and the members of the series that integrates on its series branch.
/// </summary>
public sealed record PullRequestMergeAsk(
    bool Asked, bool Judged, bool ChecksTarget = false, string? OverrideReason = null, IReadOnlyCollection<string>? SeriesMembers = null)
{
    /// <summary>The CI loop's merge: nobody asked, nothing judged.</summary>
    public static PullRequestMergeAsk Unasked { get; } = new(false, false);

    public IReadOnlyCollection<string> Members => SeriesMembers ?? [];

    /// <summary>The ask a merge-green request carries.</summary>
    public static PullRequestMergeAsk Of(Contracts.Runs.CiGreenMergeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new(request.AskedBy is not null, request.Judged, request.ChecksTarget, request.OverrideReason, request.SeriesMembers);
    }
}
