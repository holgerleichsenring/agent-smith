using System.Text.Json;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: reads the attack reviewer's JSON object out of its answer, wherever in the prose it
/// sits. The reviewer cites ids written <c>[A3]</c> and quotes spec text that may hold braces, so every
/// <c>{</c> is tried, earliest first, against every later <c>}</c>, longest first: the outermost object that
/// parses into an answer with a verdict is the answer. Null when none does.
/// </summary>
internal static class SpecAttackAnswerReader
{
    private static JsonSerializerOptions Options() => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static SpecAttackAnswer? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var ends = Positions(text, '}').Reverse().ToList();
        var options = Options();
        foreach (var start in Positions(text, '{'))
            foreach (var end in ends.Where(e => e > start))
                if (TryParse(text[start..(end + 1)], options) is { } answer)
                    return answer;
        return null;
    }

    private static SpecAttackAnswer? TryParse(string candidate, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<SpecAttackWire>(candidate, options)?.ToAnswer();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<int> Positions(string text, char c)
    {
        for (var i = text.IndexOf(c); i >= 0; i = text.IndexOf(c, i + 1)) yield return i;
    }
}
