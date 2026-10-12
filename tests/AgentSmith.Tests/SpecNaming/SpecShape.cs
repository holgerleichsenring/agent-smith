using System.Text.Json.Nodes;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa: what a later release writes — the JSON this release writes, with the named
/// properties renamed. The tests read that back under this release's readers and compare it with
/// what this release reads from its own JSON.
/// </summary>
internal static class SpecShape
{
    /// <summary>The usual spec-block renames, in both casings.</summary>
    public static readonly IReadOnlyDictionary<string, string> Ids = new Dictionary<string, string>
    {
        ["PhaseId"] = "SpecId", ["phaseId"] = "specId",
        ["Phases"] = "Specs", ["phases"] = "specs",
        ["ExecutedPhaseIds"] = "ExecutedSpecIds", ["executedPhaseIds"] = "executedSpecIds",
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
