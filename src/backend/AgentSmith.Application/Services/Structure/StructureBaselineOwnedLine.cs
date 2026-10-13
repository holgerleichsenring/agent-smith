namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-50c7: the baseline's '# owned:' line — the directories the context owned when its
/// entries were measured, tab-separated, the repository root written '.'. A later round compares
/// what it owns with THESE, so a directory a context newly claims is green on the day it is owned.
/// </summary>
public sealed class StructureBaselineOwnedLine
{
    private const string Prefix = "# owned:";
    private const string Root = ".";

    public string Render(IReadOnlyList<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);
        return string.Concat(directories.Select(d => "\t" + (d.Length == 0 ? Root : d)).Prepend(Prefix));
    }

    /// <summary>The recorded directories, or null when the baseline carries no such line.</summary>
    public IReadOnlyList<string>? Read(string? text)
    {
        var line = (text ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r'))
            .FirstOrDefault(l => l.StartsWith(Prefix, StringComparison.Ordinal));
        return line?[Prefix.Length..].Split('\t', StringSplitOptions.RemoveEmptyEntries)
            .Select(StructureWorkdir.Normalize).OfType<string>().ToList();
    }
}
