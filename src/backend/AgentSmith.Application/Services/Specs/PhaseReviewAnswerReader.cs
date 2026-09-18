using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: reads the phase reviewer's JSON out of its answer — the same scan
/// <see cref="SpecCutAnswerReader"/> takes, and for the same reason: this reviewer is taught
/// to cite ids written <c>[P3]</c>, so a bracket in its prose is ordinary and a first-to-last
/// scan parses nothing.
/// </summary>
internal static class PhaseReviewAnswerReader
{
    private static JsonSerializerOptions Options() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IReadOnlyList<PhaseFinding>? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var ends = Positions(text, ']').Reverse().ToList();
        var options = Options();
        foreach (var start in Positions(text, '[').Where(i => OpensAnAnswer(text, i)))
            foreach (var end in ends.Where(e => e > start))
                if (TryParse(text[start..(end + 1)], options) is { } findings)
                    return findings;
        return null;
    }

    private static List<PhaseFinding>? TryParse(string candidate, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<List<PhaseFindingWire>>(candidate, options)?
                .Select(w => w.ToFinding()).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool OpensAnAnswer(string text, int start)
    {
        var next = text.AsSpan(start + 1).TrimStart();
        return next.Length > 0 && next[0] is '{' or ']';
    }

    private static IEnumerable<int> Positions(string text, char c)
    {
        for (var i = text.IndexOf(c); i >= 0; i = text.IndexOf(c, i + 1)) yield return i;
    }
}
