namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-27-3532d: writes a replacement's <c>supersedes</c> line into its yaml text — the one record of the
/// retirement. Whatever top-level <c>supersedes</c> the text held is replaced, and the line stands right after the
/// <c>spec:</c> line, so the text and the draft say the same thing.
/// </summary>
public static class SpecSupersedesLine
{
    private const string Key = "supersedes:";
    private const string SpecKey = "spec:";

    public static string Stamp(string yaml, string retiredId)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentException.ThrowIfNullOrWhiteSpace(retiredId);
        var lines = Without(yaml.Split('\n'));
        var at = lines.FindIndex(l => l.StartsWith(SpecKey, StringComparison.Ordinal));
        lines.Insert(at < 0 ? 0 : at + 1, $"{Key} [{retiredId}]");
        return string.Join('\n', lines);
    }

    // The top-level supersedes key and the lines that belong to it (indented or list items) are dropped.
    private static List<string> Without(string[] lines)
    {
        var kept = new List<string>(lines.Length + 1);
        var inKey = false;
        foreach (var line in lines)
        {
            if (line.StartsWith(Key, StringComparison.Ordinal)) { inKey = true; continue; }
            if (inKey && line.Length > 0 && (char.IsWhiteSpace(line[0]) || line[0] == '-')) continue;
            inKey = false;
            kept.Add(line);
        }
        return kept;
    }
}
