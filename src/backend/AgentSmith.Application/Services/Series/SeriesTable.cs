using System.Globalization;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the series' plain status table — ticket, what, state, cost, time — and its snapshot, the text a
/// later beat compares to tell a change. Time runs from a ticket's first start to its landing, or to now.
/// </summary>
public static class SeriesTable
{
    public static IReadOnlyList<SeriesTableRow> Rows(IReadOnlyList<SeriesTicketFact> facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return [.. facts.Select(f => Row(f, facts, now))];
    }

    /// <summary>One line per ticket, "key=state"; equal snapshots mean nothing changed.</summary>
    public static string Snapshot(IReadOnlyList<SeriesTableRow> rows) =>
        string.Join("\n", rows.Select(r => $"{r.Ticket}={r.State}"));

    /// <summary>The states of an earlier snapshot by ticket.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string snapshot) =>
        snapshot.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2)).Where(p => p.Length == 2)
            .GroupBy(p => p[0], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last()[1], StringComparer.Ordinal);

    public static string Money(decimal usd) => usd <= 0 ? "–" : "$" + usd.ToString("0.0", CultureInfo.InvariantCulture);

    public static string Duration(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes:00} min" : $"{Math.Max(0, (int)span.TotalMinutes)}m";

    private static SeriesTableRow Row(SeriesTicketFact fact, IReadOnlyList<SeriesTicketFact> all, DateTimeOffset now)
    {
        var (state, kind) = SeriesTicketState.Of(fact, all);
        var time = fact.FirstStartedAt is { } start ? Duration((fact.LandedAt ?? now) - start) : "–";
        return new SeriesTableRow(fact.Ticket.Key, fact.Ticket.Title, state, kind, Money(fact.CostUsd), time);
    }
}
