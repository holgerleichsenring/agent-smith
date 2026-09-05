using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Turns a corrected set into the next revision of the artifact on the branch.
/// <para>
/// The correction has to be a REVISION and not an in-memory edit, because the derivation
/// has already written the set to the branch and commented it on the ticket: an edit that
/// stayed in the run would leave both stating a contract nobody is judged by. The cause
/// names the criteria that changed, so the diff a reviewer opens says why it changed
/// without anyone reading the run trail.
/// </para>
/// </summary>
public static class SpecReviewRevision
{
    public static SpecSet Of(SpecSet set, IReadOnlyList<CriterionReview> corrected, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(corrected);
        return set with
        {
            Revisions = [.. set.Revisions, new SpecRevision(set.Revisions.Count + 1, CauseOf(corrected), at)],
        };
    }

    /// <summary>Names what changed and why. A cause reading only "spec review" would make
    /// every one of these revisions look alike in a history a reviewer scans.</summary>
    private static string CauseOf(IReadOnlyList<CriterionReview> corrected) =>
        $"spec review: {corrected.Count} criterion(s) replaced by the observation that "
        + $"decides them — {string.Join("; ", corrected.Select(c => $"\"{c.Criterion}\""))}";
}
