using System.Text;
using System.Text.Json;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Flattens OpenAPI/Swagger JSON schema definitions into a concise
/// field list for LLM consumption.
/// </summary>
internal static class SchemaFlattener
{
    internal static void Flatten(JsonElement schema, List<string> fields, string prefix)
    {
        if (TryFlattenEnum(schema, fields, prefix)) return;

        if (!schema.TryGetProperty("properties", out var properties))
        {
            FlattenCompositions(schema, fields, prefix);
            return;
        }

        var required = CollectRequired(schema);
        FlattenProperties(properties, fields, prefix, required);
    }

    private static bool TryFlattenEnum(
        JsonElement schema, List<string> fields, string prefix)
    {
        if (!schema.TryGetProperty("enum", out var enumValues))
            return false;

        var type = schema.TryGetProperty("type", out var t) ? t.GetString() ?? "?" : "?";
        var values = string.Join(", ", enumValues.EnumerateArray()
            .Take(20).Select(e => e.ToString()));
        if (enumValues.GetArrayLength() > 20) values += ", ...";
        fields.Add($"{prefix}[enum {type}]: {values}");
        return true;
    }

    private static void FlattenCompositions(
        JsonElement schema, List<string> fields, string prefix)
    {
        foreach (var keyword in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (!schema.TryGetProperty(keyword, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("$ref", out var refVal))
                    fields.Add($"{prefix}→ {ExtractRefName(refVal.GetString())}");
                else
                    Flatten(item, fields, prefix);
            }
            return;
        }

        FlattenSimpleType(schema, fields, prefix);
    }

    private static void FlattenSimpleType(
        JsonElement schema, List<string> fields, string prefix)
    {
        if (!schema.TryGetProperty("type", out var simpleType)) return;

        var format = schema.TryGetProperty("format", out var f) ? $" ({f.GetString()})" : "";
        if (simpleType.GetString() == "array" && schema.TryGetProperty("items", out var items))
        {
            if (items.TryGetProperty("$ref", out var itemRef))
                fields.Add($"{prefix}array of {ExtractRefName(itemRef.GetString())}");
            else
                fields.Add($"{prefix}array of {(items.TryGetProperty("type", out var it) ? it.GetString() : "?")}");

            if (schema.TryGetProperty("maxItems", out var max))
                fields.Add($"{prefix}  maxItems: {max}");
        }
        else
        {
            fields.Add($"{prefix}{simpleType.GetString()}{format}");
        }
    }

    private static HashSet<string> CollectRequired(JsonElement schema)
    {
        var required = new HashSet<string>();
        if (schema.TryGetProperty("required", out var reqArr) && reqArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in reqArr.EnumerateArray())
                if (r.GetString() is { } s) required.Add(s);
        }
        return required;
    }

    private static void FlattenProperties(
        JsonElement properties, List<string> fields,
        string prefix, HashSet<string> required)
    {
        foreach (var prop in properties.EnumerateObject())
        {
            var sb = new StringBuilder();
            sb.Append($"{prefix}{prop.Name}: ");
            AppendPropertyType(sb, prop.Value);

            if (required.Contains(prop.Name))
                sb.Append(" *required");

            fields.Add(sb.ToString());
        }
    }

    private static void AppendPropertyType(StringBuilder sb, JsonElement prop)
    {
        if (prop.TryGetProperty("$ref", out var refVal))
        {
            sb.Append(ExtractRefName(refVal.GetString()));
            return;
        }

        var type = prop.TryGetProperty("type", out var pt) ? pt.GetString() ?? "?" : "?";
        var format = prop.TryGetProperty("format", out var pf) ? $" ({pf.GetString()})" : "";
        sb.Append($"{type}{format}");

        if (type == "array" && prop.TryGetProperty("items", out var ai))
        {
            if (ai.TryGetProperty("$ref", out var aiRef))
                sb.Append($" of {ExtractRefName(aiRef.GetString())}");
            else if (ai.TryGetProperty("type", out var ait))
                sb.Append($" of {ait.GetString()}");
        }

        if (prop.TryGetProperty("enum", out var ev))
        {
            var vals = string.Join(", ", ev.EnumerateArray().Take(15).Select(e => e.ToString()));
            if (ev.GetArrayLength() > 15) vals += ", ...";
            sb.Append($" enum[{vals}]");
        }

        if (prop.TryGetProperty("nullable", out var nul) && nul.GetBoolean())
            sb.Append(" nullable");

        if (prop.TryGetProperty("maxLength", out var ml))
            sb.Append($" maxLength:{ml}");

        if (prop.TryGetProperty("maxItems", out var mi))
            sb.Append($" maxItems:{mi}");
    }

    private static string ExtractRefName(string? refPath) =>
        refPath?.Split('/').LastOrDefault() ?? "?";
}
