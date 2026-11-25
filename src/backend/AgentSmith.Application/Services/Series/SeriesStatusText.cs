using System.Text;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: the series' plain table as Markdown — ticket, what, state, cost, time. 2026-11-25-e94eb:
/// only the master's prompt reads it; no conversation is posted a table.</summary>
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

    private static string Cell(string text) => text.Replace("|", "\\|").Replace("\n", " ");
}
