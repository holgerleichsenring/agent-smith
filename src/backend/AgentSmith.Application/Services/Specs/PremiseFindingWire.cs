using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: a premise finding as the checker writes it. <c>cites</c> arrives as a
/// string or as an array of strings — ffa7 found an array throwing during deserialisation and
/// losing the whole answer — so both become one citation field and the admission resolves
/// each id in turn.
/// </summary>
internal sealed record PremiseFindingWire(
    string? Premise, string? Verdict, string? Why, JsonElement? Cites = null)
{
    public PremiseFinding ToFinding() =>
        new(Premise ?? string.Empty, Verdict ?? string.Empty, Why ?? string.Empty,
            CitationText(Cites));

    private static string? CitationText(JsonElement? cites) => cites?.ValueKind switch
    {
        JsonValueKind.String => cites.Value.GetString(),
        JsonValueKind.Array => string.Join(", ", cites.Value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())),
        _ => null,
    };
}
