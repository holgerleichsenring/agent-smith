using System.Text.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-3a36b: the contradiction check's answer as the model writes it. <c>cites</c>
/// arrives as a string or a list of strings, as for <see cref="PremiseFindingWire"/>, and both
/// become one citation field.
/// </summary>
internal sealed record ContradictionCheckWire(string? Verdict, string? Why, JsonElement? Cites = null)
{
    public (string Verdict, string Why, string? Cites) ToAnswer() =>
        (Verdict?.Trim() ?? string.Empty, Why?.Trim() ?? string.Empty, CitationText(Cites));

    private static string? CitationText(JsonElement? cites) => cites?.ValueKind switch
    {
        JsonValueKind.String => cites.Value.GetString(),
        JsonValueKind.Array => string.Join(", ", cites.Value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())),
        _ => null,
    };
}
