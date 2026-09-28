using System.Text.Json.Nodes;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Retired;

/// <summary>
/// Finds where a dotted key path is SET in a configuration tree — present, and neither null nor
/// blank. A stored document serialises every property, so an unset key is present as null and
/// must not count.
/// </summary>
public sealed class ConfigKeyPathMatcher
{
    private const string AnyKey = "*";

    /// <summary>The concrete paths the pattern matches, spelled as the pattern spells its named
    /// segments and as the tree spells the keys a <c>*</c> stood for.</summary>
    public IReadOnlyList<string> SetPaths(JsonNode? root, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return [.. Walk(root, path.Split('.'), 0, [])];
    }

    private static IEnumerable<string> Walk(JsonNode? node, string[] segments, int index, string[] trail)
    {
        if (index == segments.Length)
            return IsSet(node) ? [string.Join('.', trail)] : [];
        return Children(node, segments[index])
            .SelectMany(child => Walk(child.Node, segments, index + 1, [.. trail, child.Key]));
    }

    private static IEnumerable<(string Key, JsonNode? Node)> Children(JsonNode? node, string segment) =>
        node switch
        {
            JsonObject map => map
                .Where(p => segment == AnyKey || SameKey(p.Key, segment))
                .Select(p => (segment == AnyKey ? p.Key : segment, p.Value)),
            JsonArray list when segment == AnyKey =>
                list.Select((item, i) => (i.ToString(System.Globalization.CultureInfo.InvariantCulture), item)),
            _ => [],
        };

    private static bool SameKey(string key, string segment) =>
        string.Equals(Folded(key), Folded(segment), StringComparison.OrdinalIgnoreCase);

    private static string Folded(string key) => key.Replace("_", string.Empty, StringComparison.Ordinal);

    private static bool IsSet(JsonNode? node) => node switch
    {
        null => false,
        JsonValue value when value.TryGetValue<string>(out var text) => !string.IsNullOrWhiteSpace(text),
        _ => true,
    };
}
