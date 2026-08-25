using System.Text.Json;
using System.Text.Json.Serialization;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-25-c9c7: decides whether a context document the model authored may be
/// written, and names the defect when it may not.
/// <para>
/// The judged JSON is the TYPED document projected back to JSON, not the raw
/// argument the model sent. The typed document is what the YAML on disk is
/// rendered from — its key casing is the writer's, its unknown keys are already
/// gone, and its numbers are still numbers — so the schema judges the file that
/// will exist. Judging the raw argument would reject a model whose casing never
/// reaches the file and would put the YAML-to-JSON bridge (which stringifies
/// every scalar, cut as 2026-08-25-2c7c and deliberately not fixed here) in the
/// path of every integer rule in the schema.
/// </para>
/// </summary>
public sealed class ContextDocumentGate(ContextStackImageRule stackImage, ContextSchemaRule schemaRule)
{
    public bool TryRead(JsonElement document, out ContextYamlDocument? typed, out string? defect)
    {
        typed = null;
        defect = null;
        try
        {
            typed = JsonSerializer.Deserialize<ContextYamlDocument>(document.GetRawText(), DocumentJson);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            defect = $"document is not a valid context.yaml shape — {ex.Message}";
            return false;
        }
        if (typed is not null) return true;
        defect = "document is not a valid context.yaml shape — it deserialised to null.";
        return false;
    }

    public string? Defect(ContextYamlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return stackImage.Defect(document)
            ?? schemaRule.Defect(JsonSerializer.SerializeToNode(document, DocumentJson));
    }

    private static readonly JsonSerializerOptions DocumentJson = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // The YAML writer omits nulls; the projection must too, or every absent
        // optional field would be judged as a null against its declared type.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Deserialize arch/quality/behavior (IDictionary<string, object?>) into plain
        // CLR types, not JsonElement, so the YAML serializer emits real values instead
        // of a `value_kind: String` type wrapper.
        Converters = { new InferredTypeJsonConverter() },
    };
}
