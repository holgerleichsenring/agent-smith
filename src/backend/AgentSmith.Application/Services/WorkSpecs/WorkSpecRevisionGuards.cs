using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: the two rules a master-issued revision must satisfy. Kept pure and
/// separate from the tool host so both are testable without a sandbox.
/// </summary>
public static class WorkSpecRevisionGuards
{
    /// <summary>
    /// ONE source of criteria. While a ratified expectation exists the done-section
    /// is READ-ONLY: a master free to revise the criteria it works toward, while the
    /// verdict still pairs against the original, would be working to a target nobody
    /// scores. Returns the refusal message, or null when the edit is allowed.
    /// </summary>
    public static string? RefuseDoneEdit(WorkSpec current, IReadOnlyList<string>? proposedDone)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!current.DoneIsReadOnly || proposedDone is null) return null;
        if (proposedDone.SequenceEqual(current.Done, StringComparer.Ordinal)) return null;
        return "Error: the done-criteria are the ratified acceptance contract and are "
            + "read-only for this run. Revise the requirements, constraints or assumptions "
            + "instead; changing what the run is scored against is not yours to do.";
    }

    /// <summary>
    /// Non-progress hygiene, mechanical and never a prose comparison. It applies ONLY
    /// after the first source commit: the first revision is by design written before
    /// any source edit, re-entry likewise, and "the master read the code and found the
    /// requirement does not fit" is a legitimate second revision with no commit
    /// between. The rule must not fire on exactly the case this phase wants.
    /// </summary>
    public static string? RefuseUnproductiveRevision(
        string specCommitSha, string headSha, string shaAtLastRevision)
    {
        var firstSourceCommitSeen = !string.IsNullOrEmpty(headSha)
            && !string.Equals(headSha, specCommitSha, StringComparison.Ordinal);
        if (!firstSourceCommitSeen) return null;
        if (!string.Equals(headSha, shaAtLastRevision, StringComparison.Ordinal)) return null;
        return "Error: the spec was already revised at this commit and nothing has been "
            + "committed since. Change the code first, then revise the spec if the work "
            + "showed the requirement was wrong.";
    }
}
