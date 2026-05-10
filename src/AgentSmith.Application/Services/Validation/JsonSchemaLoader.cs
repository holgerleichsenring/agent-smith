using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// Loads the four hand-written skill-output schemas from embedded resources at
/// process start, validates each against the Draft 2020-12 meta-schema, and
/// caches them by <see cref="SkillOutputSchema"/> for the validator implementations.
/// Singleton: schemas live for the process lifetime.
/// </summary>
public sealed class JsonSchemaLoader
{
    private const string ResourceNamespace = "AgentSmith.Application.Services.Validation.Schemas";
    private readonly Dictionary<SkillOutputSchema, JsonSchema> _cache;

    public JsonSchemaLoader()
    {
        _cache = new Dictionary<SkillOutputSchema, JsonSchema>
        {
            [SkillOutputSchema.Plan] = LoadFromResource("plan.schema.json"),
            [SkillOutputSchema.Diff] = LoadFromResource("diff.schema.json"),
            [SkillOutputSchema.Bootstrap] = LoadFromResource("bootstrap.schema.json"),
            [SkillOutputSchema.Observation] = LoadFromResource("observation.schema.json")
        };
    }

    public JsonSchema Get(SkillOutputSchema schema) => _cache[schema];

    private static JsonSchema LoadFromResource(string fileName)
    {
        var resourceName = $"{ResourceNamespace}.{fileName}";
        var text = ReadResource(resourceName);
        var schema = ParseOrThrow(resourceName, text);
        ValidateAgainstMetaSchema(resourceName, text);
        return schema;
    }

    private static string ReadResource(string resourceName)
    {
        var assembly = typeof(JsonSchemaLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new JsonSchemaLoadException(resourceName, "embedded schema resource not found");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static JsonSchema ParseOrThrow(string resourceName, string text)
    {
        try
        {
            return JsonSchema.FromText(text);
        }
        catch (Exception ex)
        {
            throw new JsonSchemaLoadException(resourceName, $"schema parse failed: {ex.Message}", ex);
        }
    }

    private static void ValidateAgainstMetaSchema(string resourceName, string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var result = MetaSchemas.Draft202012.Evaluate(doc.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!result.IsValid)
                throw new JsonSchemaLoadException(resourceName,
                    $"schema fails Draft 2020-12 meta-schema: {DescribeMetaSchemaErrors(result)}");
        }
        catch (JsonException ex)
        {
            throw new JsonSchemaLoadException(resourceName, $"schema not valid JSON: {ex.Message}", ex);
        }
    }

    private static string DescribeMetaSchemaErrors(EvaluationResults result)
    {
        var details = result.Details
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Take(5);
        return string.Join("; ", details);
    }
}
