using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>
/// 2026-10-12-967ab: one stored JSON text rewritten against the type that reads it — parsed,
/// walked by <see cref="SpecMemberRewriter"/> and written back only when a name moved, so a
/// document already in the spec names comes back as null ("nothing to write").
/// </summary>
public sealed class SpecRenameJson(SpecMemberRewriter members)
{
    /// <summary>The rewritten text, or null when it was empty, unreadable or already current.</summary>
    public string? Rewrite(string? json, Type type, JsonNamingPolicy? policy) =>
        Rewrite(json, node => members.Rewrite(node, type, policy));

    /// <summary>The same, with the walk supplied by the caller.</summary>
    public string? Rewrite(string? json, Func<JsonNode, bool> walk)
    {
        var node = Parse(json);
        return node is not null && walk(node) ? node.ToJsonString() : null;
    }

    /// <summary>The document, or null when it is empty or not JSON — a row nobody can read is left as it is.</summary>
    public JsonNode? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Rewrites a JSON text nested as a string member of <paramref name="owner"/>.</summary>
    public bool RewriteNested(JsonObject owner, string member, Func<string, string?> rewrite)
    {
        if (!owner.TryGetPropertyValue(member, out var value) || value is not JsonValue text
            || !text.TryGetValue<string>(out var nested)) return false;
        var rewritten = rewrite(nested);
        if (rewritten is null) return false;
        owner[member] = rewritten;
        return true;
    }

    /// <summary>Moves a member to its new name, the way the old readers resolved both: a non-null old value wins.</summary>
    public bool Move(JsonObject owner, string from, string to)
    {
        if (!owner.TryGetPropertyValue(from, out var value)) return false;
        owner.Remove(from);
        if (value is not null || !owner.ContainsKey(to)) owner[to] = value;
        return true;
    }
}
