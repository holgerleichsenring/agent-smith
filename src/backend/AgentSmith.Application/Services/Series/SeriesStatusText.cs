using System.Text;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: the series' plain table as Markdown — ticket, what, state, cost, time.</summary>
public static class SeriesStatusText
{
    public static string Table(IReadOnlyList<SeriesTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var text = new StringBuilder("| ticket | what | state | cost | time |\n|---|---|---|---|---|\n");
        foreach (var r in rows)
            text.Append($"| {Cell(r.Ticket)} | {Cell(r.What)} | {Cell(r.State)} | {r.Cost} | {r.Time} |\n");
        return text.ToString().TrimEnd();
    }

    /// <summary>Counts and spend in one line: "3 running · 1 landed · 1 red · $55 so far".</summary>
    public static string Counts(IReadOnlyList<SeriesTableRow> rows, IReadOnlyList<SeriesTicketFact> facts, decimal masterUsd)
    {
        var parts = rows.GroupBy(r => r.Kind).OrderBy(g => Order(g.Key)).Select(g => $"{g.Count()} {g.Key}");
        var spent = facts.Sum(f => f.CostUsd) + masterUsd;
        return string.Join(" · ", parts) + $" · {SeriesTable.Money(spent)} so far";
    }

    private static int Order(string kind) => kind switch
    {
        SeriesTicketState.Running => 0, SeriesTicketState.Green => 1, SeriesTicketState.Landed => 2,
        SeriesTicketState.Red => 3, SeriesTicketState.Waiting => 4, SeriesTicketState.Held => 5, _ => 6,
    };

    private static string Cell(string text) => text.Replace("|", "\\|").Replace("\n", " ");
}
