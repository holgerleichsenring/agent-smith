using System.Text;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// API security skill round: provides swagger spec + Nuclei findings as domain context.
/// Used by the api-security-scan pipeline.
/// </summary>
public sealed class ApiSkillRoundHandler(
    ILlmClientFactory llmClientFactory,
    ILogger<ApiSkillRoundHandler> logger)
    : SkillRoundHandlerBase, ICommandHandler<ApiSecuritySkillRoundContext>
{
    protected override ILogger Logger => logger;
    protected override string SkillRoundCommandName => "ApiSecuritySkillRoundCommand";

    protected override string BuildDomainSection(PipelineContext pipeline)
    {
        pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec);

        var compressedSpec = spec is not null
            ? CompressSwaggerSpec(spec)
            : "Not available";

        // Use category slices if available (p67), fall back to raw findings
        var findingsSection = BuildFindingsFromSlices(pipeline);
        if (string.IsNullOrWhiteSpace(findingsSection))
            findingsSection = BuildFindingsRaw(pipeline);

        return $"""
            ## API Security Scan Target
            Title: {spec?.Title ?? "Unknown"}
            Version: {spec?.Version ?? "Unknown"}

            ## Swagger Specification (compressed)
            {compressedSpec}

            {findingsSection}

            Analyze the findings relevant to your role.
            Focus on response schema field combinations, enum definitions, REST semantics,
            route consistency, missing constraints, and contextualize the scanner findings.
            """;
    }

    private static string BuildFindingsFromSlices(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.ApiScanFindingsSummary, out var summary))
            return string.Empty;

        pipeline.TryGet<Dictionary<string, string>>(ContextKeys.ApiScanFindingsByCategory, out var slices);
        if (slices is null || slices.Count == 0)
            return summary;

        // Get active skill name to retrieve the right slice
        pipeline.TryGet<string>(ContextKeys.ActiveSkill, out var activeSkill);
        pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, out var roles);

        var inputCategories = roles?.FirstOrDefault(r =>
            r.Name.Equals(activeSkill, StringComparison.OrdinalIgnoreCase))
            ?.Orchestration?.InputCategories;

        var skillFindings = ApiScanFindingsCompressor.GetSliceForSkill(
            activeSkill ?? "", slices, inputCategories);

        if (string.IsNullOrWhiteSpace(skillFindings))
            return summary;

        return $"""
            {summary}

            ## Relevant Findings for This Skill
            {skillFindings}
            """;
    }

    private static string BuildFindingsRaw(PipelineContext pipeline)
    {
        pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);
        pipeline.TryGet<SpectralResult>(ContextKeys.SpectralResult, out var spectral);

        var nucleiFindings = nuclei is not null && nuclei.Findings.Count > 0
            ? string.Join("\n", nuclei.Findings.Select(f =>
                $"  [{f.Severity.ToUpperInvariant()}] {f.TemplateId}: {f.Name} — {f.MatchedUrl}"
                + (f.Description is not null ? $"\n    {f.Description}" : "")))
            : "No findings from Nuclei scan";

        var spectralFindings = spectral is not null && spectral.Findings.Count > 0
            ? string.Join("\n", spectral.Findings.Select(f =>
                $"  [{f.Severity.ToUpperInvariant()}] {f.Code}: {f.Message} — {f.Path} (line {f.Line})"))
            : "No findings from Spectral lint";

        return $"""
            ## Nuclei Scan Findings ({nuclei?.Findings.Count ?? 0} total)
            {nucleiFindings}

            ## Spectral Lint Findings ({spectral?.Findings.Count ?? 0} total, {spectral?.ErrorCount ?? 0} errors, {spectral?.WarnCount ?? 0} warnings)
            {spectralFindings}
            """;
    }

    internal static string CompressSwaggerSpec(SwaggerSpec spec)
    {
        var sb = new StringBuilder();

        // Extract and deduplicate schemas from RawJson
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

        // Security schemes
        if (spec.SecuritySchemes.Count > 0)
        {
            sb.AppendLine("### Security Schemes");
            foreach (var s in spec.SecuritySchemes)
                sb.AppendLine($"  {s.Name}: {s.Type} (in: {s.In ?? "n/a"}, scheme: {s.Scheme ?? "n/a"})");
            sb.AppendLine();
        }

        // Endpoints with compressed response info
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

        return sb.ToString();
    }

    internal static Dictionary<string, List<string>> ExtractSchemas(string rawJson)
    {
        var schemas = new Dictionary<string, List<string>>();

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            // OpenAPI 3.x: components.schemas, Swagger 2.0: definitions
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
                FlattenSchema(schema.Value, fields, "");
                schemas[schema.Name] = fields;
            }
        }
        catch (JsonException) { }

        return schemas;
    }

    private static void FlattenSchema(JsonElement schema, List<string> fields, string prefix)
    {
        // Handle enum at this level
        if (schema.TryGetProperty("enum", out var enumValues))
        {
            var type = schema.TryGetProperty("type", out var t) ? t.GetString() ?? "?" : "?";
            var values = string.Join(", ", enumValues.EnumerateArray()
                .Take(20).Select(e => e.ToString()));
            if (enumValues.GetArrayLength() > 20) values += ", ...";
            fields.Add($"{prefix}[enum {type}]: {values}");
            return;
        }

        if (!schema.TryGetProperty("properties", out var properties))
        {
            // allOf / oneOf / anyOf — flatten first item
            foreach (var keyword in new[] { "allOf", "oneOf", "anyOf" })
            {
                if (schema.TryGetProperty(keyword, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (item.TryGetProperty("$ref", out var refVal))
                            fields.Add($"{prefix}→ {ExtractRefName(refVal.GetString())}");
                        else
                            FlattenSchema(item, fields, prefix);
                    }
                    return;
                }
            }

            // Simple type without properties
            if (schema.TryGetProperty("type", out var simpleType))
            {
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
            return;
        }

        // Collect required fields
        var required = new HashSet<string>();
        if (schema.TryGetProperty("required", out var reqArr) && reqArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in reqArr.EnumerateArray())
                if (r.GetString() is { } s) required.Add(s);
        }

        foreach (var prop in properties.EnumerateObject())
        {
            var sb = new StringBuilder();
            sb.Append($"{prefix}{prop.Name}: ");

            if (prop.Value.TryGetProperty("$ref", out var refVal))
            {
                sb.Append(ExtractRefName(refVal.GetString()));
            }
            else
            {
                var type = prop.Value.TryGetProperty("type", out var pt) ? pt.GetString() ?? "?" : "?";
                var format = prop.Value.TryGetProperty("format", out var pf) ? $" ({pf.GetString()})" : "";
                sb.Append($"{type}{format}");

                if (type == "array" && prop.Value.TryGetProperty("items", out var ai))
                {
                    if (ai.TryGetProperty("$ref", out var aiRef))
                        sb.Append($" of {ExtractRefName(aiRef.GetString())}");
                    else if (ai.TryGetProperty("type", out var ait))
                        sb.Append($" of {ait.GetString()}");
                }

                if (prop.Value.TryGetProperty("enum", out var ev))
                {
                    var vals = string.Join(", ", ev.EnumerateArray().Take(15).Select(e => e.ToString()));
                    if (ev.GetArrayLength() > 15) vals += ", ...";
                    sb.Append($" enum[{vals}]");
                }

                if (prop.Value.TryGetProperty("nullable", out var nul) && nul.GetBoolean())
                    sb.Append(" nullable");

                if (prop.Value.TryGetProperty("maxLength", out var ml))
                    sb.Append($" maxLength:{ml}");

                if (prop.Value.TryGetProperty("maxItems", out var mi))
                    sb.Append($" maxItems:{mi}");
            }

            if (required.Contains(prop.Name))
                sb.Append(" *required");

            fields.Add(sb.ToString());
        }
    }

    internal static string? ExtractSchemaRef(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            // Look through content types for a $ref
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
            // Swagger 2.0: schema directly
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

                // Try content → first content-type → schema → $ref
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
                            break; // Take first content-type only
                        }
                    }
                }
                // Swagger 2.0: schema directly on response
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

    public async Task<CommandResult> ExecuteAsync(
        ApiSecuritySkillRoundContext context, CancellationToken cancellationToken)
    {
        var llmClient = llmClientFactory.Create(context.AgentConfig);
        return await ExecuteRoundAsync(
            context.SkillName, context.Round, context.Pipeline, llmClient, cancellationToken);
    }
}
