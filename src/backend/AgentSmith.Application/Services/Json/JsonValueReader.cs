using System.Text.Json;

namespace AgentSmith.Application.Services.Json;

/// <summary>
/// p0426: reads a value out of MODEL-produced JSON by checking its KIND first.
/// <para>
/// <c>JsonElement.TryGetInt32</c> reads like a safe accessor and is not one: it returns
/// false only when a NUMBER does not fit, and THROWS on any other kind. Run 27 ended at
/// step 12 because an analyzer answered <c>file_count: null</c> — not a wrong answer, just
/// "I do not know", which is a normal thing for a model to write.
/// </para>
/// <para>
/// The producer here is a language model, so every field is optional, every type is a
/// suggestion, and a parser that trusts either is a run-ending crash waiting for its
/// input. Safety belongs in the accessor, not in the discipline of twenty call sites.
/// </para>
/// </summary>
public static class JsonValueReader
{
    public static int Int32(JsonElement element, string property, int fallback = 0) =>
        Number(element, property, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : fallback;

    public static long Int64(JsonElement element, string property, long fallback = 0) =>
        Number(element, property, out var value) && value.TryGetInt64(out var parsed)
            ? parsed
            : fallback;

    public static double Double(JsonElement element, string property, double fallback = 0) =>
        Number(element, property, out var value) && value.TryGetDouble(out var parsed)
            ? parsed
            : fallback;

    public static bool Bool(JsonElement element, string property, bool fallback = false) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    /// <summary>Null and absent are the same answer: the model did not say.</summary>
    public static string? Text(JsonElement element, string property, string? fallback = null) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : fallback;

    /// <summary>The element ITSELF as a number — for arrays of bare values.</summary>
    public static int Int32(JsonElement element, int fallback = 0) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsed)
            ? parsed
            : fallback;

    private static bool Number(JsonElement element, string property, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out value)
            && value.ValueKind == JsonValueKind.Number;
    }
}
