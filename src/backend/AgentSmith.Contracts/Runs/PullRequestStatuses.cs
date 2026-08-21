namespace AgentSmith.Contracts.Runs;

/// <summary>
/// The vocabulary of <c>PullRequestOutcomeEvent.Status</c> — the per-repo pull-request
/// state as it is persisted on the run row (p0347's <c>Runs.PullRequestsJson</c>) and
/// served verbatim to the dashboard. p0490 extends it past the open attempt: a pull
/// request the run also finished, and one the platform declined to finish.
/// </summary>
public static class PullRequestStatuses
{
    /// <summary>The pull request was opened and is waiting.</summary>
    public const string Opened = "opened";

    /// <summary>The repo had nothing to commit, so no pull request was opened.</summary>
    public const string NoChanges = "no_changes";

    /// <summary>The commit, push or open attempt failed; <c>Reason</c> says how.</summary>
    public const string Failed = "failed";

    /// <summary>p0490: the run completed (merged) the pull request it opened.</summary>
    public const string Completed = "completed";

    /// <summary>p0490: the platform refused the completion — a branch policy, a required
    /// reviewer or a required build. The pull request stays OPEN and <c>Reason</c> names
    /// what declined it. Not a failure: the pull request is init's output, not its
    /// success criterion.</summary>
    public const string CompletionRefused = "completion_refused";

    /// <summary>p0501: the platform accepted the instruction to finish the pull request
    /// and will merge it ITSELF once the policy it is waiting on — typically a required
    /// integration build — is satisfied. Distinct from <see cref="Completed"/>, which has
    /// already merged, and from <see cref="CompletionRefused"/>, which needs a human:
    /// nobody has to come back to an armed pull request.</summary>
    public const string CompletionArmed = "completion_armed";

    /// <summary>p0490: the repo HAS a pull request on the platform — opened, completed,
    /// armed, or left open by a refused completion — as opposed to none at all. Before
    /// init learned to finish its own pull requests, <see cref="Opened"/> answered this
    /// question on its own; it no longer does, and a surface that still asks it that way
    /// loses every link the moment a run merges what it opened.</summary>
    public static bool HasPullRequest(string? status) =>
        status is Opened or Completed or CompletionArmed or CompletionRefused;

    /// <summary>p0501: the pull request is waiting for a PERSON. An armed one is waiting
    /// too, but for a build, so counting it here would send the operator to look at
    /// something that is already handling itself.</summary>
    public static bool NeedsAHuman(string? status) =>
        status is Opened or CompletionRefused;
}
