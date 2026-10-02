using System.Text;
using System.Text.Json;

namespace AgentSmith.Application.Services.Design;

/// <summary>
/// 2026-10-01-7f7ab: a Figma nodes response as the trimmed summary design_read returns — the
/// file's name, version and last modification, then one line per node, indented by depth,
/// under a character budget. What the budget cuts is stated with its count, never dropped in
/// silence. Raw JSON never passes through: a file response runs to megabytes.
/// </summary>
public static class FigmaNodeSummary
{
    /// <summary>Renders <paramref name="nodesResponse"/>; bound variable ids resolve through <paramref name="variableNames"/>.</summary>
    public static string Render(
        JsonElement nodesResponse, IReadOnlyDictionary<string, string> variableNames, int budget)
    {
        var text = new StringBuilder()
            .AppendLine($"file: {FigmaJson.Str(nodesResponse, "name")}")
            .AppendLine($"version: {FigmaJson.Str(nodesResponse, "version")}")
            .AppendLine($"last modified: {FigmaJson.Str(nodesResponse, "lastModified")}");
        var shown = 0;
        var total = 0;
        foreach (var entry in FigmaJson.Members(FigmaJson.Obj(nodesResponse, "nodes")))
        {
            if (FigmaJson.Obj(entry.Value, "document") is not { } document)
            {
                text.AppendLine($"node {entry.Name}: not in this file version");
                continue;
            }
            var line = new FigmaNodeLine(Names(entry.Value, "components"), Names(entry.Value, "styles"), variableNames);
            Walk(document, 0, line, text, budget, ref shown, ref total);
        }
        if (shown < total)
            text.AppendLine($"[truncated: {total - shown} of {total} nodes not shown — budget {budget} characters;"
                + " call design_read on a child node's link or id to read further]");
        return text.ToString().TrimEnd();
    }

    private static void Walk(
        JsonElement node, int depth, FigmaNodeLine line, StringBuilder text, int budget,
        ref int shown, ref int total)
    {
        if (node.TryGetProperty("visible", out var visible) && visible.ValueKind == JsonValueKind.False) return;
        total++;
        var rendered = $"{new string(' ', depth * 2)}- {line.Render(node)}";
        if (shown == total - 1 && text.Length + rendered.Length + 1 <= budget)
        {
            text.AppendLine(rendered);
            shown++;
        }
        foreach (var child in FigmaJson.Arr(node, "children"))
            Walk(child, depth + 1, line, text, budget, ref shown, ref total);
    }

    private static Dictionary<string, string> Names(JsonElement entry, string member) =>
        FigmaJson.Members(FigmaJson.Obj(entry, member))
            .Select(m => (Id: m.Name, Label: FigmaJson.Str(m.Value, "name")))
            .Where(m => m.Label is not null)
            .ToDictionary(m => m.Id, m => m.Label!, StringComparer.Ordinal);
}
