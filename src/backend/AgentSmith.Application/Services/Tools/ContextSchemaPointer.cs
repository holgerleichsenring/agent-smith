using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Validation;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-26-167c: resolves a JSON Pointer into the context schema DOCUMENT, so a
/// rejection can quote the rule that broke — its values, its pattern, its
/// suggestions — rather than only naming a keyword the model cannot look up.
/// <para>
/// The pointer is the validator's evaluation path, which addresses the schema node
/// carrying the keyword; the keyword itself is read off the node the caller gets back.
/// </para>
/// </summary>
public sealed class ContextSchemaPointer(ContextSchemaProvider schema)
{
    public JsonNode? Resolve(string pointer)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        JsonNode? node = schema.Document;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (node is null) return null;
            node = Step(node, Unescape(segment));
        }
        return node;
    }

    // An evaluation path can address an element of a keyword that holds an array
    // (anyOf, allOf), so a numeric segment is an index, not a property name.
    private static JsonNode? Step(JsonNode node, string segment) => node switch
    {
        JsonObject obj => obj[segment],
        JsonArray array when int.TryParse(segment, out var index)
            && index >= 0 && index < array.Count => array[index],
        _ => null,
    };

    private static string Unescape(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
               .Replace("~0", "~", StringComparison.Ordinal);
}
