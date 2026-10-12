using System.Text.Json.Nodes;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa/967ab: what the release before the rename wrote — the JSON this release writes,
/// with the spec-named properties put back under their phase names. The tests read that back under
/// this release's readers and compare it with what this release reads from its own JSON.
/// </summary>
internal static class SpecShape
{
    /// <summary>The usual spec-block renames, in both casings, spec name to the old phase name.</summary>
    public static readonly IReadOnlyDictionary<string, string> Ids = new Dictionary<string, string>
    {
        ["SpecId"] = "PhaseId", ["specId"] = "phaseId",
        ["Specs"] = "Phases", ["specs"] = "phases",
        ["ExecutedSpecIds"] = "ExecutedPhaseIds", ["executedSpecIds"] = "executedPhaseIds",
    };

    public static string Of(string json, IReadOnlyDictionary<string, string>? renames = null)
    {
        var node = JsonNode.Parse(json)!;
        Rename(node, renames ?? Ids);
        return node.ToJsonString();
    }

    private static void Rename(JsonNode? node, IReadOnlyDictionary<string, string> renames)
    {
        switch (node)
        {
            case JsonArray array:
                foreach (var item in array) Rename(item, renames);
                break;
            case JsonObject obj:
                foreach (var (name, value) in obj.ToList())
                {
                    Rename(value, renames);
                    if (!renames.TryGetValue(name, out var renamed)) continue;
                    obj.Remove(name);
                    obj[renamed] = value;
                }
                break;
        }
    }
}
