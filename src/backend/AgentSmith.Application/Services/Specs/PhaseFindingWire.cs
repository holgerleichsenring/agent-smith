using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: a phase-review finding as the reviewer writes it. <c>line</c> arrives as
/// a number or as a string ("42", "line 42"); <c>cites</c> as a string or an array, as
/// <see cref="CutFindingWire"/> already handles. Both are normalised here so the admission
/// reads one shape.
/// </summary>
internal sealed record PhaseFindingWire(
    string? Repository, string? Path, JsonElement? Line, string? Rule, string? Why,
    JsonElement? Cites = null)
{
    public PhaseFinding ToFinding() =>
        new(Repository ?? string.Empty, Path ?? string.Empty, LineNumber(Line),
            Rule ?? string.Empty, Why ?? string.Empty, CitationText(Cites));

    private static int LineNumber(JsonElement? line) => line?.ValueKind switch
    {
        JsonValueKind.Number => line.Value.TryGetInt32(out var n) ? n : 0,
        JsonValueKind.String => Digits(line.Value.GetString()),
        _ => 0,
    };

    private static int Digits(string? text) =>
        int.TryParse(
            new string((text ?? string.Empty).SkipWhile(c => !char.IsAsciiDigit(c))
                .TakeWhile(char.IsAsciiDigit).ToArray()),
            out var n) ? n : 0;

    private static string? CitationText(JsonElement? cites) => cites?.ValueKind switch
    {
        JsonValueKind.String => cites.Value.GetString(),
        JsonValueKind.Array => string.Join(", ", cites.Value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString())),
        _ => null,
    };
}
