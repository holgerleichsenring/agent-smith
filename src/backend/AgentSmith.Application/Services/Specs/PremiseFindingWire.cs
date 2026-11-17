using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: a premise finding as the checker writes it. <c>cites</c> arrives as a
/// string or as an array of strings — ffa7 found an array throwing during deserialisation and
/// losing the whole answer — so both become one citation field and the admission resolves
/// each id in turn. 2026-11-17-c47ea: <c>breaks</c> names the done criterion the false premise breaks, or null.
/// </summary>
internal sealed record PremiseFindingWire(
    string? Premise, string? Verdict, string? Why, JsonElement? Cites = null, string? Breaks = null)
{
    public PremiseFinding ToFinding() =>
        new(Premise ?? string.Empty, Verdict ?? string.Empty, Why ?? string.Empty,
            CitationText(Cites), Breaks: string.IsNullOrWhiteSpace(Breaks) ? null : Breaks);

    private static string? CitationText(JsonElement? cites) => cites?.ValueKind switch
    {
        JsonValueKind.String => cites.Value.GetString(),
        JsonValueKind.Array => string.Join(", ", cites.Value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())),
        _ => null,
    };
}
