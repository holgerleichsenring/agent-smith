using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// 2026-08-25-c9c7: loads the context schema (the same
/// <c>.agentsmith/context.schema.json</c> this repository validates its own
/// contexts against) from the embedded resource at process start and validates
/// it against its declared Draft-07 meta-schema. Singleton — the schema lives
/// for the process lifetime.
/// <para>
/// <see cref="Document"/> is the schema as data, so a rejection can quote the
/// rule that was broken (the enum's values, the pattern, the length cap) rather
/// than only naming the keyword the model cannot look up.
/// </para>
/// </summary>
public sealed class ContextSchemaProvider
{
    private const string ResourceName =
        "AgentSmith.Application.Services.Validation.Schemas.context.schema.json";

    public JsonSchema Schema { get; }

    public JsonNode Document { get; }

    public ContextSchemaProvider()
    {
        var text = ReadResource();
        Schema = ParseOrThrow(text);
        Document = JsonNode.Parse(text)
            ?? throw new JsonSchemaLoadException(ResourceName, "schema parsed to a null document");
        ValidateAgainstMetaSchema(text);
    }

    private static string ReadResource()
    {
        var assembly = typeof(ContextSchemaProvider).Assembly;
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
