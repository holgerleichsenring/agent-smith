using System.Text.Json.Nodes;

namespace AgentSmith.Application.Services.Pocs.Argo;

/// <summary>
/// 2026-11-23-c81ec: whether a stored ApplicationSet already holds the render — compared on spec.generators,
/// spec.template and spec.syncPolicy only, and there only on the keys agent smith renders: the server adds defaults
/// (status, metadata it owns, empty fields) a whole-object comparison would read as a change every time. Arrays must
/// match element by element and in length; scalars by their JSON text.
/// </summary>
public sealed class PocApplicationSetMatch
{
    private static readonly string[] Compared = ["generators", "template", "syncPolicy"];

    public bool Holds(JsonObject wanted, JsonObject? stored)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        if (stored?["spec"] is not JsonObject storedSpec || wanted["spec"] is not JsonObject wantedSpec) return false;
        return Compared.All(key => wantedSpec[key] is not { } part || Contains(part, storedSpec[key]));
    }

    private static bool Contains(JsonNode wanted, JsonNode? stored) => wanted switch
    {
        JsonObject o => stored is JsonObject s && o.All(p => p.Value is null || Contains(p.Value, s[p.Key])),
        JsonArray a => stored is JsonArray s && a.Count == s.Count && a.Select((item, i) => item is null || Contains(item, s[i])).All(x => x),
        _ => stored is not null && wanted.ToJsonString() == stored.ToJsonString(),
    };
}
