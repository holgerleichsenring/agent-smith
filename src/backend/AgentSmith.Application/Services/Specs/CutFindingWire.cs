using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: a cut finding as the reviewer writes it. <c>cites</c> arrives as a string
/// or as an array of strings; both become one citation field, each id a candidate the reader
/// of the finding resolves in turn.
/// </summary>
internal sealed record CutFindingWire(
    string PhaseId, string Criterion, string Problem, string Why,
    string? ConflictsWith = null, JsonElement? Cites = null)
{
    public CutFinding ToFinding() =>
        new(PhaseId, Criterion, Problem, Why, ConflictsWith, CitationText(Cites));

    private static string? CitationText(JsonElement? cites) => cites?.ValueKind switch
    {
        JsonValueKind.String => cites.Value.GetString(),
        JsonValueKind.Array => string.Join(", ", cites.Value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())),
        _ => null,
    };
}
