namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: MEMORY.md with each entry's label rewritten from its standing — the file a person reads in
/// the repository follows the evidence. Every other line, and every line's order, stays as written.
/// <para>
/// 2026-11-26-462aa: MEMORY.md merges with <c>merge=union</c>, which keeps both versions of a line two branches
/// changed differently and brings back a line one side deleted. So only the first line per entry name is kept, and
/// an entry line whose file beside MEMORY.md is not among the listed files is dropped.
/// </para>
/// </summary>
public sealed class MemoryIndexLabels
{
    public string Relabel(
        string index, IReadOnlyDictionary<string, MemoryStanding> standings, IReadOnlySet<string>? files = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(standings);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var raw in index.Replace("\r\n", "\n").Split('\n'))
        {
            if (MemoryIndexLine.TryParse(raw) is not { } line) { lines.Add(raw); continue; }
            if (!seen.Add(line.Name) || Gone(line, files)) continue;
            lines.Add(line.Format((standings.GetValueOrDefault(line.Name) ?? MemoryStanding.None(line.Name)).Label));
        }
        return string.Join('\n', lines);
    }

    // Only a file beside MEMORY.md is judged, and only by a listing that shows MEMORY.md itself — a listing that
    // failed or read another directory judges nothing.
    private static bool Gone(MemoryIndexLine line, IReadOnlySet<string>? files) =>
        files is not null && files.Contains(Contracts.Services.ProjectMetaPaths.MemoryIndexFile)
        && !line.File.Contains('/') && !files.Contains(line.File);
}
