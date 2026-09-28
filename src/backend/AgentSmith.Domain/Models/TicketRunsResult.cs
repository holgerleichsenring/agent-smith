namespace AgentSmith.Domain.Models;

/// <summary>One run THIS FRAMEWORK made against a ticket.</summary>
/// <param name="Phases">The phases it worked, in the order it worked them.</param>
/// <param name="PullRequests">What it opened, as the run recorded it — which is not the same as
/// what the board shows: a pull request opened by hand is invisible here.</param>
public sealed record TicketRun(
    string RunId, string Project, string Status, DateTimeOffset StartedAt,
    IReadOnlyList<string> Phases, IReadOnlyList<string> PullRequests);

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
public sealed record TicketRunsResult(IReadOnlyList<TicketRun> Runs)
{
    /// <summary>Where these facts came from — said in the answer, not assumed by the reader.</summary>
    public string Source => "this framework's own run record";

    public static TicketRunsResult None { get; } = new([]);
}
