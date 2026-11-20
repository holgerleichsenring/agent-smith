using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-19-1b72a: the one line a pause or a resume says in the conversation, from what it found and did — a run
/// actually under way finishes (or was cancelled), a queued start is held, a parked run waits for its answer; a resume
/// names only what started, the red CIs it handed back to the CI loop and the red tickets that wait for a retry.</summary>
public static class SeriesPauseText
{
    public const string Holds = "no new ticket starts — a queued start waits for the resume — though a start already under way may still "
        + "begin, and an automatic fix of a red CI waits too; a retry or rework you ask for still runs.";

    public static bool Running(SeriesTicketFact f) =>
        f is { LatestRunId: not null, LatestFinished: false } && f.LatestStatus is not (RunStatuses.Queued or RunStatuses.WaitingForInput);

    public static bool CiRed(SeriesTicketFact f) => f is { Landed: false, LatestStatus: RunStatuses.Success, CiState: CiStates.Red };

    public static string Pause(bool already, IReadOnlyList<SeriesTicketFact> all, string runs) =>
        (already ? "The series stays paused. " : $"The series is paused: {Holds} ") + runs
        + Named(all.Where(f => f is { Landed: false, LatestStatus: RunStatuses.Queued }), "'s queued start is held", "' queued starts are held")
        + Named(all.Where(f => f.LatestStatus == RunStatuses.WaitingForInput), " waits for your answer", " wait for your answers")
        + "Say resume, or press Resume, to go on.";

    public static string Resume(IReadOnlyList<SeriesTicketFact> started, IReadOnlyList<SeriesTicketFact> all)
    {
        var ci = all.Where(CiRed).ToList();
        var red = all.Where(f => !CiRed(f) && SeriesTicketState.Of(f, all).Kind == SeriesTicketState.Red).ToList();
        return "The series resumes" + (started.Count == 0 ? "." : $" and starts {Keys(started)}.")
            + (ci.Count == 0 ? string.Empty : $" The red CI of {Keys(ci)} goes back to the CI loop.")
            + (red.Count == 0 ? string.Empty : $" {Keys(red)} {(red.Count == 1 ? "is red and waits" : "are red and wait")} for a retry; "
                + $"say retry to run {(red.Count == 1 ? "it" : "them")} again.");
    }

    public static string Keys(IEnumerable<SeriesTicketFact> facts) => string.Join(", ", facts.Select(f => f.Ticket.Key));

    private static string Named(IEnumerable<SeriesTicketFact> facts, string one, string many)
    {
        var list = facts.ToList();
        return list.Count == 0 ? string.Empty : $"{Keys(list)}{(list.Count == 1 ? one : many)}. ";
    }
}
