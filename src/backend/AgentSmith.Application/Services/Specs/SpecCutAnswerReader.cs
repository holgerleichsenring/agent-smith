using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: reads the cut reviewer's JSON out of its answer.
/// <para>
/// The reviewer is taught to cite ids written <c>[R3]</c>, so a bracket in its prose is
/// ordinary — and a scan from the first <c>[</c> to the last <c>]</c> then fails to parse and
/// the whole review reads as "could not be taken". Every <c>[</c> that opens an array of
/// objects or an empty array is tried, earliest first, against every later <c>]</c>, longest
/// first: the outermost array that parses is the answer.
/// </para>
/// </summary>
internal static class SpecCutAnswerReader
{
    // snake_case is what the prompt asks for, and case-insensitivity alone does not bridge
    // an underscore — the same trap ships_code fell into.
    private static JsonSerializerOptions Options() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IReadOnlyList<CutFinding>? Read(string? text)
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

    private static List<CutFinding>? TryParse(string candidate, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<List<CutFindingWire>>(candidate, options)?
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
