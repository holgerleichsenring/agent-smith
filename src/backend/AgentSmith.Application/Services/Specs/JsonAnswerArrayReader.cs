using System.Text.Json;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: reads a JSON array of findings out of a model's answer, wherever in the
/// prose it sits.
/// <para>
/// Every reader of this shape teaches the model to cite ids written <c>[R3]</c>, so a bracket
/// in its prose is ordinary — and a scan from the first <c>[</c> to the last <c>]</c> then
/// fails to parse and the whole answer reads as "could not be taken". Every <c>[</c> that
/// opens an array of objects or an empty array is tried, earliest first, against every later
/// <c>]</c>, longest first: the outermost array that parses is the answer.
/// </para>
/// <para>
/// 2026-09-17-0e79c: the scan itself belongs to no one caller. A second reader with its own
/// wire shape (the premise check) uses the same one rather than growing a copy that drifts.
/// </para>
/// </summary>
internal static class JsonAnswerArrayReader
{
    // snake_case is what these prompts ask for, and case-insensitivity alone does not bridge
    // an underscore — the same trap ships_code fell into.
    private static JsonSerializerOptions Options() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static IReadOnlyList<T>? Read<TWire, T>(string? text, Func<TWire, T> toFinding)
    {
        ArgumentNullException.ThrowIfNull(toFinding);
        if (string.IsNullOrWhiteSpace(text)) return null;
        var ends = Positions(text, ']').Reverse().ToList();
        var options = Options();
        foreach (var start in Positions(text, '[').Where(i => OpensAnAnswer(text, i)))
            foreach (var end in ends.Where(e => e > start))
                if (TryParse(text[start..(end + 1)], options, toFinding) is { } findings)
                    return findings;
        return null;
    }

    private static List<T>? TryParse<TWire, T>(
        string candidate, JsonSerializerOptions options, Func<TWire, T> toFinding)
    {
        try
        {
            return JsonSerializer.Deserialize<List<TWire>>(candidate, options)?
                .Select(toFinding).ToList();
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
