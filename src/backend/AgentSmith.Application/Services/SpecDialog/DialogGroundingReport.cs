using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-19-4c1f: the one line the grounding step leaves on its run-step row. Absence
/// has three shapes and an operator reading the row has to tell them apart: a repository
/// that names no location was never asked, a location that carries no such file answered
/// and has none, and a read that was refused says nothing at all about whether it is there.
/// </summary>
internal static class DialogGroundingReport
{
    public static string Compose(IReadOnlyList<DialogGrounding> grounded)
    {
        var reached = grounded.Where(g => g is { Located: true, Unreachable: null }).ToList();
        var parts = new List<string>
        {
            $"Grounded the design turn on {reached.Count} of {grounded.Count} scoped repo(s): "
            + $"{reached.Sum(g => g.Contexts.Documents.Count)} context document(s), "
            + $"{reached.Sum(g => g.Principles.Documents.Count)} principles file(s)",
        };
        Add(parts, "no location", [.. grounded.Where(g => !g.Located).Select(g => g.Repo)]);
        Add(parts, "no file", [.. reached.SelectMany(Absent)]);
        Add(parts, "could not be read", [.. grounded.SelectMany(Refused)]);
        return string.Join(". ", parts) + ".";
    }

    private static IEnumerable<string> Absent(DialogGrounding grounding)
    {
        if (grounding.Contexts.IsAbsent)
            yield return $"{grounding.Repo}/{ProjectMetaPaths.Contexts}/*/{ProjectMetaPaths.ContextYamlFile}";
        if (grounding.Principles.IsAbsent)
            yield return $"{grounding.Repo}/{ProjectMetaPaths.Principles}";
    }

    private static IEnumerable<string> Refused(DialogGrounding grounding)
    {
        if (grounding.Unreachable is not null)
            yield return $"{grounding.Repo}/{ProjectMetaPaths.Contexts} ({grounding.Unreachable})";
        foreach (var entry in grounding.Contexts.Unreadable) yield return $"{grounding.Repo}/{entry}";
        foreach (var entry in grounding.Principles.Unreadable) yield return $"{grounding.Repo}/{entry}";
    }

    private static void Add(List<string> parts, string label, IReadOnlyList<string> items)
    {
        if (items.Count > 0) parts.Add($"{label}: {string.Join(", ", items)}");
    }
}
