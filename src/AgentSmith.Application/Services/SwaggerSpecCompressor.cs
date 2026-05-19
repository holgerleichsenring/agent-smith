using System.Text.Json;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Default <see cref="ISwaggerSpecCompressor"/> implementation. Strips example payloads,
/// truncates verbose descriptions to <see cref="DescriptionMaxChars"/>, and drops
/// component schemas no path references (transitively) when the input spec exceeds
/// <see cref="SizeThresholdChars"/>. Specs below the threshold pass through unchanged
/// so small / well-behaved projects pay zero processing cost.
/// </summary>
public sealed class SwaggerSpecCompressor(ILogger<SwaggerSpecCompressor> logger) : ISwaggerSpecCompressor
{
    /// <summary>100k chars — picked so typical 10–50k specs are pass-through but
    /// near-truncation cases (SampleA's 291k) get compressed. See phase p0147c.</summary>
    public const int DefaultSizeThresholdChars = 100_000;

    /// <summary>Descriptions over 240 chars get truncated. LLMs use descriptions to
    /// disambiguate similar endpoints; full-paragraph docstrings rarely add signal.</summary>
    private const int DescriptionMaxChars = 240;

    public int SizeThresholdChars => DefaultSizeThresholdChars;

    public SwaggerSpec Compress(SwaggerSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.RawJson.Length <= SizeThresholdChars)
            return spec;

        string compressedJson;
        try
        {
            compressedJson = CompressJson(spec.RawJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "SwaggerSpec compression skipped: RawJson failed to parse ({Chars} chars)",
                spec.RawJson.Length);
            return spec;
        }

        logger.LogInformation(
            "SwaggerSpec compressed: {OldChars} → {NewChars} chars ({Ratio:P0})",
            spec.RawJson.Length, compressedJson.Length,
            spec.RawJson.Length == 0 ? 0d : (double)compressedJson.Length / spec.RawJson.Length);

        return spec with { RawJson = compressedJson };
    }

    private static string CompressJson(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var referencedSchemas = CollectReferencedSchemaNames(root);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteFiltered(root, writer, path: "$", referencedSchemas);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static HashSet<string> CollectReferencedSchemaNames(JsonElement root)
    {
        var refs = new HashSet<string>(StringComparer.Ordinal);

        // Seed from paths section ($refs the API surface actually uses)
        if (root.TryGetProperty("paths", out var paths))
            CollectRefsRecursive(paths, refs);

        // Walk transitive closure: a referenced schema may itself reference others.
        if (!root.TryGetProperty("components", out var components) ||
            !components.TryGetProperty("schemas", out var schemas))
        {
            // OpenAPI 2 fallback
            if (root.TryGetProperty("definitions", out schemas))
            { }
            else return refs;
        }

        var pending = new Queue<string>(refs);
        while (pending.Count > 0)
        {
            var name = pending.Dequeue();
            if (!schemas.TryGetProperty(name, out var schema))
                continue;

            var transitiveRefs = new HashSet<string>(StringComparer.Ordinal);
            CollectRefsRecursive(schema, transitiveRefs);
            foreach (var r in transitiveRefs)
            {
                if (refs.Add(r))
                    pending.Enqueue(r);
            }
        }

        return refs;
    }

    private static void CollectRefsRecursive(JsonElement element, HashSet<string> sink)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("$ref") && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var name = ExtractRefName(prop.Value.GetString());
                        if (name is not null) sink.Add(name);
                    }
                    else
                    {
                        CollectRefsRecursive(prop.Value, sink);
                    }
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectRefsRecursive(item, sink);
                break;
        }
    }

    private static string? ExtractRefName(string? refPath)
    {
        if (string.IsNullOrEmpty(refPath)) return null;
        var slash = refPath.LastIndexOf('/');
        return slash < 0 || slash == refPath.Length - 1 ? null : refPath[(slash + 1)..];
    }

    private static void WriteFiltered(
        JsonElement element,
        Utf8JsonWriter writer,
        string path,
        HashSet<string> referencedSchemas)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var isComponentsSchemas = ComponentsSchemasPath.IsMatch(path);
                var isDefinitions = path == "$.definitions";
                foreach (var prop in element.EnumerateObject())
                {
                    // Drop example payloads entirely — biggest size win.
                    if (prop.NameEquals("example") || prop.NameEquals("examples"))
                        continue;

                    // Truncate verbose descriptions; keep first sentence.
                    if (prop.NameEquals("description") && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var desc = prop.Value.GetString() ?? "";
                        writer.WriteString(prop.Name, TruncateDescription(desc));
                        continue;
                    }

                    // Drop unreferenced component schemas.
                    if ((isComponentsSchemas || isDefinitions) && !referencedSchemas.Contains(prop.Name))
                        continue;

                    writer.WritePropertyName(prop.Name);
                    WriteFiltered(prop.Value, writer, $"{path}.{prop.Name}", referencedSchemas);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteFiltered(item, writer, path + "[]", referencedSchemas);
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static readonly Regex ComponentsSchemasPath = new(
        @"^\$\.components\.schemas$", RegexOptions.Compiled);

    internal static string TruncateDescription(string description)
    {
        if (description.Length <= DescriptionMaxChars) return description;

        // Try to cut at the first sentence boundary within the limit.
        var window = description.AsSpan(0, DescriptionMaxChars);
        var dot = window.LastIndexOf('.');
        var cut = dot > DescriptionMaxChars / 2 ? dot + 1 : DescriptionMaxChars;
        return description[..cut].TrimEnd() + " …";
    }
}
