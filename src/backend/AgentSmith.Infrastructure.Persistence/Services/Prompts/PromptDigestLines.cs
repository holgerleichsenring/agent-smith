using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentSmith.Infrastructure.Persistence.Services.Prompts;

/// <summary>
/// 2026-11-18-e06bd: one line from a stored message, as PromptBlocks rendered it: a tool result is
/// "[tool result #id]" plus its text, a tool call "[tool call NAME #id]" plus its JSON arguments, anything else text.
/// The fed line names the tool a result answers by the call that asked for it; the decided line lists the tool calls
/// an answer made, or its first line of text.
/// </summary>
public static partial class PromptDigestLines
{
    public const int MaxChars = 160; // not a limit: the width of one display line; the full text is the prompt read

    /// <summary>The last message a call was sent, given that message and the messages before it (newest last).</summary>
    public static string? Fed(string role, string content, IEnumerable<string> earlier)
    {
        var results = ResultMarker().Matches(content);
        if (results.Count == 0) return Cut($"{role}: {FirstLine(content)}");
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var call in earlier.SelectMany(e => CallMarker().Matches(e))) names[call.Groups["id"].Value] = call.Groups["name"].Value;
        var first = results[0];
        var end = results.Count > 1 ? results[1].Index : content.Length;
        var text = content[(first.Index + first.Length)..end];
        var name = names.GetValueOrDefault(first.Groups["id"].Value, "tool");
        var more = results.Count > 1 ? $" (+{results.Count - 1} more)" : string.Empty;
        return Cut($"{name} result: {FirstLine(text)}", more);
    }

    /// <summary>What an answer did: its tool calls with their arguments, or its first line of text.</summary>
    public static string? Decided(string content)
    {
        var calls = CallWithArgs().Matches(content);
        if (calls.Count == 0) return FirstLine(content) is { Length: > 0 } line ? Cut(line) : null;
        var said = calls.Select(c => $"{c.Groups["name"].Value} {Args(c.Groups["args"].Value)}".TrimEnd()).ToList();
        return Cut(string.Join("; ", said));
    }

    /// <summary>A JSON argument object as its values, space-separated; anything else as it is.</summary>
    public static string Args(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return json.Trim();
            return string.Join(" ", doc.RootElement.EnumerateObject().Select(p => Value(p.Value)).Where(v => v.Length > 0));
        }
        catch (JsonException)
        {
            return json.Trim();
        }
    }

    private static string Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => FirstLine(value.GetString() ?? string.Empty),
        JsonValueKind.Null => string.Empty,
        _ => value.GetRawText(),
    };

    private static string FirstLine(string text) =>
        text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;

    private static string Cut(string line, string tail = "")
    {
        var room = MaxChars - tail.Length;
        return (line.Length <= room ? line : line[..(room - 1)].TrimEnd() + "…") + tail;
    }

    [GeneratedRegex(@"\[tool result #(?<id>[^\]]*)\]")]
    private static partial Regex ResultMarker();

    [GeneratedRegex(@"\[tool call (?<name>\S+) #(?<id>[^\]]*)\]")]
    private static partial Regex CallMarker();

    [GeneratedRegex(@"\[tool call (?<name>\S+) #[^\]]*\]\r?\n(?<args>[^\r\n]*)")]
    private static partial Regex CallWithArgs();
}
