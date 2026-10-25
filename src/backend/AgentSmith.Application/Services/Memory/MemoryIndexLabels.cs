namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: MEMORY.md with each entry's label rewritten from its standing — the file a person reads in
/// the repository follows the evidence. Every other line, and every line's order, stays as written.
/// </summary>
public sealed class MemoryIndexLabels
{
    public string Relabel(string index, IReadOnlyDictionary<string, MemoryStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(standings);
        var lines = index.Replace("\r\n", "\n").Split('\n').Select(raw => MemoryIndexLine.TryParse(raw) is { } line
            ? line.Format((standings.GetValueOrDefault(line.Name) ?? MemoryStanding.None(line.Name)).Label)
            : raw);
        return string.Join('\n', lines);
    }
}
