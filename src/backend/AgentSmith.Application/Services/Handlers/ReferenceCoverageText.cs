using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-04-96a0c: how much of an upload earlier turns opened, under its address in a design turn's prompt. Earlier
/// turns' file content does not reach this turn — only their replies and the note do — so the line says that too,
/// and lists the first unopened files, so a master cannot claim it has read what this turn holds none of.
/// </summary>
internal static class ReferenceCoverageText
{
    private const string Indent = "  ";

    internal static string Under(ReferenceCoverage? coverage)
    {
        if (coverage is null) return string.Empty;
        var lines = new List<string>();
        if (coverage.Files > 0)
        {
            lines.Add($"Earlier turns of this conversation opened {coverage.Opened.Count} of the {coverage.Files} files "
                + "read_file can read (read_file only; grep_in_tree and run_in_reference are not counted). Their content is not "
                + "in this turn; your notes (read_notes) carry what you kept.");
            lines.Add(Unopened(coverage));
        }
        if (coverage.Unreadable > 0)
            lines.Add($"{coverage.Unreadable} files read_file cannot open (binary or over 1 MB) are not counted.");
        return string.Concat(lines.Select(line => $"\n{Indent}{line}"));
    }

    private static string Unopened(ReferenceCoverage coverage)
    {
        if (coverage.UnopenedCount == 0) return $"All {coverage.Files} opened earlier.";
        var shown = coverage.Unopened;
        var more = coverage.UnopenedCount - shown.Count;
        return "Not opened yet: " + string.Join(", ", shown.Select(path => $"`{path}`"))
            + (more > 0 ? $" … and {more} more." : ".");
    }
}
