using System.Text.Json.Nodes;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// The keys an input tree SETS that a stored tree does not carry. Keys compare ignoring case
/// and underscores, because stored documents spell the C# names (<c>IsEnabled</c> for
/// <c>is_enabled</c>). A scalar standing for an object (<c>primary_provider: claude</c>) is
/// kept when the object is; list items compare position by position.
/// </summary>
public sealed class StoredKeyDiff
{
    /// <summary>YAML key -> the stored property it is kept under, where the two names differ.</summary>
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["repo"] = "ref" };

    public IReadOnlyList<string> Missing(JsonNode? input, JsonNode? stored) => [.. Walk(input, stored, "")];

    private static IEnumerable<string> Walk(JsonNode? input, JsonNode? stored, string path) => input switch
    {
        JsonObject map when stored is JsonObject kept => map.SelectMany(p => Key(p.Key, p.Value, kept, path)),
        JsonArray list when stored is JsonArray kept => list.SelectMany((item, i) =>
            i < kept.Count ? Walk(item, kept[i], $"{path}.{i}") : []),
        _ => [],
    };

    private static IEnumerable<string> Key(string key, JsonNode? value, JsonObject kept, string path)
    {
        var at = path.Length == 0 ? key : $"{path}.{key}";
        var match = kept.FirstOrDefault(p => Same(p.Key, key) || Aliases.TryGetValue(key, out var alias) && Same(p.Key, alias));
        if (match.Key is null) return IsSet(value) ? [at] : [];
        return Walk(value, match.Value, at);
    }

    private static bool Same(string a, string b) =>
        string.Equals(a.Replace("_", ""), b.Replace("_", ""), StringComparison.OrdinalIgnoreCase);

    private static bool IsSet(JsonNode? node) => node switch
    {
        null => false,
        JsonObject map => map.Count > 0,
        JsonArray list => list.Count > 0,
        JsonValue v when v.TryGetValue<string>(out var text) => !string.IsNullOrWhiteSpace(text),
        _ => true,
    };
}
