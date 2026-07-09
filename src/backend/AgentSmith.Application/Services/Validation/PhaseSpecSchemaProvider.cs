using System.Text.Json;
using Json.Schema;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// p0315b: loads the phase-spec JSON schema (the same
/// <c>.agentsmith/phase-spec.schema.json</c> operators author phases against)
/// from the embedded resource at process start and validates it against its
/// declared Draft-07 meta-schema. Singleton — the schema lives for the
/// process lifetime; SpecDraftValidator evaluates drafted specs against it.
/// </summary>
public sealed class PhaseSpecSchemaProvider
{
    private const string ResourceName =
        "AgentSmith.Application.Services.Validation.Schemas.phase-spec.schema.json";

    public JsonSchema Schema { get; }

    public PhaseSpecSchemaProvider()
    {
        var text = ReadResource();
        Schema = ParseOrThrow(text);
        ValidateAgainstMetaSchema(text);
    }

    private static string ReadResource()
    {
        var assembly = typeof(PhaseSpecSchemaProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new JsonSchemaLoadException(ResourceName, "embedded schema resource not found");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static JsonSchema ParseOrThrow(string text)
    {
        try
        {
            return JsonSchema.FromText(text);
        }
        catch (Exception ex)
        {
            throw new JsonSchemaLoadException(ResourceName, $"schema parse failed: {ex.Message}", ex);
        }
    }

    private static void ValidateAgainstMetaSchema(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var result = MetaSchemas.Draft7.Evaluate(doc.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (!result.IsValid)
                throw new JsonSchemaLoadException(ResourceName, "schema fails the Draft-07 meta-schema");
        }
        catch (JsonException ex)
        {
            throw new JsonSchemaLoadException(ResourceName, $"schema not valid JSON: {ex.Message}", ex);
        }
    }
}
