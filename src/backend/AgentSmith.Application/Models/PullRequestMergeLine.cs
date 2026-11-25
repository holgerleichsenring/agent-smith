using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-11-23-31dce: what a merge did to one repository's pull request — the host's outcome (a pull request never tried
/// because an earlier one was not merged counts as refused) and the sentence the run's result says for it, which names
/// the repository and the host's or guard's reason. A merge-green run a person asked for hands these back to the page.
/// </summary>
public sealed record PullRequestMergeLine(string Repo, PullRequestCompletionOutcome Outcome, string Text)
{
    /// <summary>2026-11-25-96aeb: a refusal an override passes — an unlanded prerequisite or a base the host could not say.</summary>
    public bool Overridable { get; init; }

    /// <summary>2026-11-25-96aeb: the refusals a judged merge went past for this repository; empty when none stood.</summary>
    public IReadOnlyList<string> Overrode { get; init; } = [];

    /// <summary>The line of a repository the merge did not merge, with why.</summary>
    public static PullRequestMergeLine Refused(string repo, string reason) =>
        new(repo, PullRequestCompletionOutcome.Refused, $"{repo}: {reason}");

    /// <summary>The line a completion answers: merged, armed, or still open with the host's reason.</summary>
    public static PullRequestMergeLine Of(string repo, string? url, PullRequestCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);
        return new(repo, completion.Outcome, completion.Outcome switch
        {
            PullRequestCompletionOutcome.Merged => $"{repo}: merged ({url})",
            PullRequestCompletionOutcome.Armed => $"{repo}: armed — {completion.Reason}",
            _ => $"{repo}: still open — {completion.Reason}",
        });
    }
}
