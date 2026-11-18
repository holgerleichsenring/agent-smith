using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207e: the panel's view of a series from its record and its tickets' facts.</summary>
public static class SeriesPanel
{
    public static SeriesRunView Of(SeriesRunRecord record, IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now,
        ResolvedProject project, DialogueGlobalConfig dialogue)
    {
        ArgumentNullException.ThrowIfNull(record);
        var rows = SeriesTable.Rows(facts, now);
        var path = SeriesCriticalPath.Of(facts);
        var left = SeriesCriticalPath.Left(facts, path.Count);
        var question = record.Question is { Answer: null } waiting ? waiting.Text : null;
        return new SeriesRunView(record.Id, record.State, Head(record, question), facts.Count(f => f.Landed), facts.Count,
            SeriesTable.Duration((record.FinishedAt ?? now) - record.StartedAt),
            SeriesTable.Money(facts.Sum(f => f.CostUsd) + record.MasterCostUsd),
            left is { } l ? $"{(now + l).UtcDateTime:HH:mm} UTC" : null, Now(rows),
            [.. path.Select(f => f.Ticket.Key)], left is { } span ? $"{path.Count} tickets, about {SeriesTable.Duration(span)} left" : null,
            SeriesPolicy.Line(project, dialogue), question);
    }

    private static string Head(SeriesRunRecord record, string? question) => record.State switch
    {
        SeriesRunStates.Done => "series done",
        SeriesRunStates.Stopped => "series stopped",
        SeriesRunStates.Verifying => question is null ? "series checking on main" : "series checking on main · 1 question for you",
        _ => question is null ? "series running" : "series running · 1 question for you",
    };

    // What moves now: every ticket working, green, red or waiting on an answer, then the held ones.
    private static IReadOnlyList<SeriesNowRow> Now(IReadOnlyList<SeriesTableRow> rows) =>
        [.. rows.Where(r => r.Kind is not (SeriesTicketState.Landed or SeriesTicketState.NotStarted))
            .OrderBy(r => r.Kind == SeriesTicketState.Held ? 1 : 0).Select(r => new SeriesNowRow(r.Ticket, r.State, r.Kind))];
}
