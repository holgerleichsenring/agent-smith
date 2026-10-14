namespace AgentSmith.Domain.Models;

/// <summary>One run THIS FRAMEWORK made against a ticket.</summary>
/// <param name="Cause">Why the attempt started, as the run recorded it; null on a run that never
/// recorded one.</param>
/// <param name="PendingQuestion">The question a parked run waits on, verbatim.</param>
/// <param name="Specs">The specs it worked, in the order it worked them.</param>
/// <param name="PullRequests">What it opened, as the run recorded it — which is not the same as
/// what the board shows: a pull request opened by hand is invisible here.</param>
public sealed record TicketRun(
    string RunId, string Project, string Pipeline, string Status, DateTimeOffset StartedAt,
    TicketRunCause? Cause, string? PendingQuestion,
    IReadOnlyList<TicketRunSpec> Specs, IReadOnlyList<string> PullRequests);

/// <summary>
/// 2026-09-28-1da5d: what this framework did about a ticket, kept apart from what the tracker
/// shows against it.
/// <para>
/// "The run says it is done" and "the board says it is done" are not one claim. A run this
/// framework finished is a fact about this framework; a branch somebody pushed by hand and a pull
/// request opened outside a run are facts about the tracker and invisible here. Merging the two
/// would produce an answer more confident than either source, so the answer names its own.
/// </para>
/// </summary>
/// <param name="PullRequestReviews">2026-10-13-9a59o: every pull request record of the ticket with
/// its newest review entries — what reviewers said, as this framework recorded it.</param>
public sealed record TicketRunsResult(
    IReadOnlyList<TicketRun> Runs, IReadOnlyList<TicketPullRequestReview> PullRequestReviews)
{
    /// <summary>Where these facts came from — said in the answer, not assumed by the reader.</summary>
    public string Source => "this framework's own run record";

    public static TicketRunsResult None { get; } = new([], []);
}
