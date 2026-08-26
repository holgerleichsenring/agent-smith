using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Validation;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-26-167c: accepts a single value wherever the context schema declares a
/// list of them, rewriting it to a one-element list before anything judges it.
/// <para>
/// The shipped project-bootstrap master hands the model a worked example whose
/// fields are the wrong TYPE — <c>"type": "Angular SPA"</c> and
/// <c>"style": "Layered"</c> where the schema declares arrays. The master lives in
/// the skills repository and reaches this build as a SHA256-verified tarball, so
/// correcting it is a release there and a pin bump here; it cannot be the condition
/// for a run completing. The schema is the side that gives, and a single value read
/// as a list of one is the reading a person would give it anyway.
/// </para>
/// <para>
/// The schema is the only source: a node is a list because the schema says
/// <c>"type": "array"</c> there, never because a field name was hard-coded here.
/// </para>
/// </summary>
public sealed class ContextSingleValueNormaliser(ContextSchemaProvider schema)
{
    public JsonNode? Normalise(JsonNode? document) =>
        Rebuild(document, schema.Document as JsonObject);

    // Rebuilds rather than mutates: a JsonNode belongs to one parent, so moving a
    // value into a fresh array in place throws.
    private JsonNode? Rebuild(JsonNode? node, JsonObject? rule)
    {
        if (node is null) return null;
        if (rule is null) return node.DeepClone();
        if (DeclaresArray(rule) && node is not JsonArray)
            return new JsonArray(Rebuild(node, Items(rule)));
        return node switch
        {
            JsonArray array => RebuildArray(array, Items(rule)),
            JsonObject obj => RebuildObject(obj, rule),
            _ => node.DeepClone(),
        };
    }

    private JsonArray RebuildArray(JsonArray array, JsonObject? items) =>
        [.. array.Select(element => Rebuild(element, items))];

    private JsonObject RebuildObject(JsonObject obj, JsonObject rule)
    {
        var rebuilt = new JsonObject();
        foreach (var (key, value) in obj) rebuilt[key] = Rebuild(value, Child(rule, key));
        return rebuilt;
    }

    // quality.naming declares `"type": ["object", "string"]`, so the keyword is not
    // always a single string — reading it as one would throw on that node.
    private static bool DeclaresArray(JsonObject rule) =>
        rule["type"] is JsonValue value
        && value.TryGetValue<string>(out var declared)
        && declared == "array";

    private static JsonObject? Items(JsonObject rule) => rule["items"] as JsonObject;

    private static JsonObject? Child(JsonObject rule, string key) =>
        rule["properties"]?[key] as JsonObject ?? rule["additionalProperties"] as JsonObject;
}
