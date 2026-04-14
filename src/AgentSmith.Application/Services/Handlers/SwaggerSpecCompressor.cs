using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Compresses a SwaggerSpec into a concise text representation
/// for LLM prompt injection. Extracts schemas, endpoints, security schemes.
/// </summary>
internal sealed class SwaggerSpecCompressor
{
    internal string Compress(SwaggerSpec spec)
    {
        var sb = new StringBuilder();

        var schemas = ExtractSchemas(spec.RawJson);
        if (schemas.Count > 0)
        {
            sb.AppendLine("### Schemas");
            foreach (var (name, fields) in schemas)
            {
                sb.AppendLine($"{name}:");
                foreach (var field in fields)
                    sb.AppendLine($"  {field}");
            }
            sb.AppendLine();
        }

        if (spec.SecuritySchemes.Count > 0)
        {
            sb.AppendLine("### Security Schemes");
            foreach (var s in spec.SecuritySchemes)
                sb.AppendLine($"  {s.Name}: {s.Type} (in: {s.In ?? "n/a"}, scheme: {s.Scheme ?? "n/a"})");
            sb.AppendLine();
        }

        AppendEndpoints(sb, spec);
        return sb.ToString();
    }

    private static void AppendEndpoints(StringBuilder sb, SwaggerSpec spec)
    {
        sb.AppendLine($"### Endpoints ({spec.Endpoints.Count})");
        foreach (var ep in spec.Endpoints)
        {
            sb.Append($"{ep.Method} {ep.Path}");
            if (ep.RequiresAuth) sb.Append(" [auth]");
            if (ep.OperationId is not null) sb.Append($" ({ep.OperationId})");
            sb.AppendLine();

            if (ep.Parameters.Count > 0)
            {
                var paramList = string.Join(", ", ep.Parameters.Select(p =>
                    $"{p.Name}:{p.Type ?? "?"} [{p.In}]{(p.Required ? "*" : "")}"));
                sb.AppendLine($"  Params: {paramList}");
            }

            var requestRef = ExtractSchemaRef(ep.RequestBodySchema);
            if (requestRef is not null)
                sb.AppendLine($"  Request: {requestRef}");

            var responseRefs = ExtractResponseRefs(ep.ResponseSchema);
            if (responseRefs.Count > 0)
                sb.AppendLine($"  Responses: {string.Join(", ", responseRefs.Select(r => $"{r.Key}→{r.Value}"))}");
        }
    }

    internal static Dictionary<string, List<string>> ExtractSchemas(string rawJson)
    {
        var schemas = new Dictionary<string, List<string>>();

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            JsonElement schemasElement;
            if (root.TryGetProperty("components", out var components)
                && components.TryGetProperty("schemas", out schemasElement))
            { }
            else if (root.TryGetProperty("definitions", out schemasElement))
            { }
            else return schemas;

            foreach (var schema in schemasElement.EnumerateObject())
            {
                var fields = new List<string>();
                SchemaFlattener.Flatten(schema.Value, fields, "");
                schemas[schema.Name] = fields;
            }
        }
        catch (JsonException) { }

        return schemas;
    }

    internal static string? ExtractSchemaRef(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("content", out var content))
            {
                foreach (var ct in content.EnumerateObject())
                {
                    if (ct.Value.TryGetProperty("schema", out var schema))
                    {
                        if (schema.TryGetProperty("$ref", out var refVal))
                            return ExtractRefName(refVal.GetString());
                        if (schema.TryGetProperty("type", out var t))
                            return t.GetString();
                    }
                }
            }
            if (doc.RootElement.TryGetProperty("schema", out var s2))
            {
                if (s2.TryGetProperty("$ref", out var refVal))
                    return ExtractRefName(refVal.GetString());
            }
        }
        catch (JsonException) { }

        return null;
    }

    internal static Dictionary<string, string> ExtractResponseRefs(string? json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);

            foreach (var response in doc.RootElement.EnumerateObject())
            {
                var code = response.Name;

                if (response.Value.TryGetProperty("content", out var content))
                {
                    foreach (var ct in content.EnumerateObject())
                    {
                        if (ct.Value.TryGetProperty("schema", out var schema))
                        {
                            if (schema.TryGetProperty("$ref", out var refVal))
                                result[code] = ExtractRefName(refVal.GetString())!;
                            else if (schema.TryGetProperty("type", out var t))
                                result[code] = t.GetString() ?? "?";
                            break;
                        }
                    }
                }
                else if (response.Value.TryGetProperty("schema", out var s2))
                {
                    if (s2.TryGetProperty("$ref", out var refVal))
                        result[code] = ExtractRefName(refVal.GetString())!;
                    else if (s2.TryGetProperty("type", out var t))
                        result[code] = t.GetString() ?? "?";
                }
            }
        }
        catch (JsonException) { }

        return result;
    }

    private static string ExtractRefName(string? refPath) =>
        refPath?.Split('/').LastOrDefault() ?? "?";
}
