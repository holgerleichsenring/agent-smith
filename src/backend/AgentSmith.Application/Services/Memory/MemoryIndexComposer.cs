using System.Text;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: the index a master is shown — each entry with its standing and record, holding entries
/// first, then unconfirmed ones, in file order within each; disputed entries are left out and counted in one
/// closing line, so a memory the runs turned against is never handed out as guidance.
/// </summary>
public sealed class MemoryIndexComposer
{
    public string Compose(string index, IReadOnlyDictionary<string, MemoryStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(standings);
        var other = new List<string>();
        var entries = new List<(MemoryIndexLine Line, MemoryStanding Standing)>();
        foreach (var raw in index.Replace("\r\n", "\n").Split('\n'))
        {
            if (MemoryIndexLine.TryParse(raw) is { } line)
                entries.Add((line, standings.GetValueOrDefault(line.Name) ?? MemoryStanding.None(line.Name)));
            else if (raw.Trim().Length > 0) other.Add(raw);
        }
        var shown = entries.Where(e => e.Standing.Label == MemoryStandingLabels.Holding)
            .Concat(entries.Where(e => e.Standing.Label == MemoryStandingLabels.Unconfirmed));
        var sb = new StringBuilder();
        foreach (var line in other) sb.Append(line).Append('\n');
        foreach (var (line, standing) in shown) sb.Append(line.Format($"{standing.Label} · {standing.Record}")).Append('\n');
        var disputed = entries.Count(e => e.Standing.Label == MemoryStandingLabels.Disputed);
        if (disputed > 0)
            sb.Append($"\n{disputed} disputed {(disputed == 1 ? "memory is" : "memories are")} left out: the runs' evidence turned against {(disputed == 1 ? "it" : "them")}.\n");
        return sb.ToString().Trim();
    }
}
