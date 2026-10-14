using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>Reads one ADF node — its type, its children and its attributes — tolerant of every missing field.</summary>
internal static class JiraAdfNode
{
    public static string Type(JsonElement node) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
            ? type.GetString() ?? string.Empty
            : string.Empty;

    public static IEnumerable<JsonElement> Children(JsonElement node) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray()
            : [];

    public static string? Text(JsonElement node) => Str(node, "text");

    public static string? Attr(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty("attrs", out var attrs) ? Str(attrs, name) : null;

    public static int? IntAttr(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty("attrs", out var attrs) && attrs.ValueKind == JsonValueKind.Object
        && attrs.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
