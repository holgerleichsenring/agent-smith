using System.Text.Json;
using System.Text.Json.Nodes;
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
/// reaches the file.
/// </para>
/// <para>
/// 2026-08-26-167c: both rules RUN and both REPORT. The gate used to return the
/// image rule's defect OR the schema's, so the commonest first-round failure hid
/// every schema defect behind one line and the second round started guessing again.
/// </para>
/// </summary>
public sealed class ContextDocumentGate(
    ContextStackImageRule stackImage,
    ContextSchemaRule schemaRule,
    ContextDefectReport report,
    ContextSingleValueNormaliser normaliser)
{
    public bool TryRead(JsonElement document, out ContextYamlDocument? typed, out string? defect)
    {
        typed = null;
        defect = null;
        try
        {
            // A single value where a list is declared is normalised BEFORE anything
            // reads the document — the shipped prompt writes that shorthand, and
            // deserialising it used to throw and hand the model a CLR type name.
            var normalised = normaliser.Normalise(JsonNode.Parse(document.GetRawText()));
            typed = normalised.Deserialize<ContextYamlDocument>(DocumentJson);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            defect = $"{Where(ex)}: the value is not the shape context.yaml declares there. "
                   + "Send the document shape the tool description names.";
            return false;
        }
        if (typed is not null) return true;
        defect = "document is not a valid context.yaml shape — it deserialised to null.";
        return false;
    }

    public string? Defect(ContextYamlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var projected = JsonSerializer.SerializeToNode(document, DocumentJson);
        return report.Compose(stackImage.Defect(document), schemaRule.Defects(projected));
    }

    // The validator's own path, not the CLR type it failed to build — the model can
    // act on "/stack/testing" and cannot act on IReadOnlyList`1[System.String].
    private static string Where(Exception ex) =>
        ex is JsonException { Path: { Length: > 0 } path } ? path : "/";

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
