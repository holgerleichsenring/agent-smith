using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Server.Services.Config;

/// <summary>
/// p0353: the Config Studio "Changes" view DTO. The stored <see cref="ConfigChange"/>
/// carries the diff as before/after JSON plus enum discriminators; the dashboard renders
/// a field-level diff keyed by the studio's route-style entity kind. Returning the raw
/// record (timestamp/entityType/operation/beforeJson/afterJson) left the client reading a
/// `fields` array that was never sent — it dereferenced `undefined` and crashed the whole
/// view. Mapping here to the shape the client already expects is the fix.
/// </summary>
public sealed record ConfigChangeView(
    string Id,
    string Actor,
    DateTimeOffset TimestampUtc,
    string EntityKind,
    string EntityId,
    string Action,
    IReadOnlyList<ConfigChangeFieldView> Fields,
    bool Reverted)
{
    public static ConfigChangeView From(ConfigChange c) => new(
        c.Id, c.Actor, c.Timestamp, KindOf(c.EntityType), c.EntityId,
        ActionOf(c.Operation), DiffFields(c.BeforeJson, c.AfterJson), c.Reverted);

    // The studio addresses kinds route-style (plural), matching the client's ConfigEntityKind.
    private static string KindOf(ConfigEntityType type) => type switch
    {
        ConfigEntityType.Agent => "agents",
        ConfigEntityType.Tracker => "trackers",
        ConfigEntityType.Repo => "repos",
        ConfigEntityType.Project => "projects",
        ConfigEntityType.McpServer => "mcp-servers",
        ConfigEntityType.Secret => "secrets",
        ConfigEntityType.Connection => "connections",
        ConfigEntityType.Settings => "settings",
        _ => type.ToString().ToLowerInvariant()
    };

    private static string ActionOf(ConfigChangeOperation op) => op switch
    {
        ConfigChangeOperation.Create => "create",
        ConfigChangeOperation.Update => "update",
        ConfigChangeOperation.Delete => "delete",
        _ => op.ToString().ToLowerInvariant()
    };

    // Top-level field diff from the before/after JSON. Create has no before, Delete no
    // after; Update shows only the keys whose value text changed. Nested objects render
    // as their JSON text — enough to see WHAT changed in the audit trail.
    private static IReadOnlyList<ConfigChangeFieldView> DiffFields(string? beforeJson, string? afterJson)
    {
        var before = Parse(beforeJson);
        var after = Parse(afterJson);
        var fields = new List<ConfigChangeFieldView>();
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            before.TryGetValue(key, out var b);
            after.TryGetValue(key, out var a);
            if (!string.Equals(b, a, StringComparison.Ordinal))
                fields.Add(new ConfigChangeFieldView(key, b, a));
        }
        return fields;
    }

    private static Dictionary<string, string?> Parse(string? json)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return map;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;
            foreach (var prop in doc.RootElement.EnumerateObject())
                map[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString()
                    : prop.Value.GetRawText();
        }
        catch (JsonException)
        {
            // A malformed audit blob shows an empty diff rather than crashing the view.
        }
        return map;
    }
}

/// <summary>p0353: one changed field in a <see cref="ConfigChangeView"/> (null = absent side).</summary>
public sealed record ConfigChangeFieldView(string Field, string? Before, string? After);
