using System.Text.Json;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: reads the series-contract reviewer's JSON object out of its answer, wherever in the prose it
/// sits — the outermost object that reads as an answer with a verdict, as <see cref="SpecAttackAnswerReader"/> scans.
/// Every field goes through <see cref="SpecJsonReader"/>; a line written as null or as a string never throws.
/// </summary>
internal static class SeriesContractAnswerReader
{
    public static SeriesContractAnswer? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var ends = Positions(text, '}').Reverse().ToList();
        foreach (var start in Positions(text, '{'))
            foreach (var end in ends.Where(e => e > start))
                if (TryParse(text[start..(end + 1)]) is { } answer)
                    return answer;
        return null;
    }

    private static SeriesContractAnswer? TryParse(string candidate)
    {
        try
        {
            using var document = JsonDocument.Parse(candidate);
            return Of(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SeriesContractAnswer? Of(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && SpecAttackWire.VerdictOf(SpecJsonReader.ReadString(root, "verdict")) is { } verdict
            ? new SeriesContractAnswer(verdict, [.. SpecJsonReader.ReadObjects(root, "cuts").Select(Item)],
                [.. SpecJsonReader.ReadObjects(root, "findings").Select(Item)])
            : null;

    private static SeriesContractItem Item(JsonElement e) => new(Text(e, "spec_id"), Text(e, "entry"),
        new SpecAttackItem(Text(e, "register")?.ToLowerInvariant(), Text(e, "quote"), Text(e, "replacement"),
            Text(e, "text"), Text(e, "repository") ?? string.Empty, Text(e, "path"),
            SpecFindingWire.LineNumber(Field(e, "line")), SpecFindingWire.CitationText(Field(e, "cites"))));

    private static string? Text(JsonElement e, string name) =>
        SpecJsonReader.ReadString(e, name) is { Length: > 0 } text ? text : null;

    private static JsonElement? Field(JsonElement e, string name) =>
        SpecJsonReader.TryGet(e, name, out var value) ? value : null;

    private static IEnumerable<int> Positions(string text, char c)
    {
        for (var i = text.IndexOf(c); i >= 0; i = text.IndexOf(c, i + 1)) yield return i;
    }
}
