using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-19-1b72a: the one-line messages a beat writes into a conversation with a Series pane — only what turned to
/// need the operator since the last beat: a ticket that failed, a question (in its own words), a pull request that waits
/// for a person's merge. Everything else the pane shows. A ticket that turned red wakes the master, so when the master
/// speaks in the same beat its line says the failure; and under merge: on_ci_green a red CI is the CI loop's to fix, so
/// it is said only once the loop handed it back (its fix ended red).
/// </summary>
public static class SeriesNeedsYou
{
    public static IReadOnlyList<string> Lines(SeriesBeat beat, ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(project);
        var before = SeriesTable.Parse(beat.Record.LastTable);
        var masterSpoke = beat.MasterLine is { Length: > 0 };
        var rows = beat.Rows;
        return [.. rows.Select((row, i) => (Row: row, Fact: beat.Facts[i], Was: before.GetValueOrDefault(row.Ticket)))
            .Where(x => x.Was != x.Row.State)
            .Select(x => Line(x.Row, x.Fact, x.Was, project, masterSpoke)).OfType<string>()];
    }

    private static string? Line(SeriesTableRow row, SeriesTicketFact fact, string? was, ResolvedProject project, bool masterSpoke) => row.Kind switch
    {
        SeriesTicketState.Red when masterSpoke => null,
        SeriesTicketState.Red => Failed(row.Ticket, fact, project),
        SeriesTicketState.Waiting => fact.PendingQuestion is { } question
            ? $"{row.Ticket} asks: {question.Text}" : $"{row.Ticket} waits for your input on its ticket.",
        SeriesTicketState.Green when project.Merge.When == MergeWhen.Manual && was?.StartsWith("green", StringComparison.Ordinal) != true
            => $"{row.Ticket}'s pull request waits for your merge.",
        _ => null,
    };

    private static string? Failed(string key, SeriesTicketFact fact, ResolvedProject project)
    {
        if (fact is { LatestStatus: RunStatuses.Success, CiState: CiStates.Red })
            return project.Merge.When == MergeWhen.OnCiGreen ? null : $"{key}'s CI is red: {fact.CiFailure ?? "a check failed"}.";
        return fact.EndDetail is { Length: > 0 } why ? $"{key} failed: {why}." : $"{key} {fact.LatestStatus ?? "failed"}.";
    }
}
