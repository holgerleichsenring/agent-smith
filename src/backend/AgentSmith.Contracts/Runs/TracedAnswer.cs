using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0427: renders a model answer for the trace and parses it back for a replay.
/// <para>
/// An answer is TEXT AND CALLS. Recording only the text made every tool-calling answer
/// record as empty, which is exactly the half of a run a replay has to reproduce. One codec
/// owns both directions, so a recording and its replay cannot drift apart.
/// </para>
/// </summary>
public static class TracedAnswer
{
    private const string CallMarker = "=== trace tool-call ";
    private const string MarkerEnd = " ===";

    public static string Render(ChatResponse response) =>
        Render(response.Text ?? string.Empty, response.Messages.SelectMany(m => m.Contents));

    public static string Render(string text, IEnumerable<AIContent> contents)
    {
        var rendered = new StringBuilder(text);
        foreach (var call in contents.OfType<FunctionCallContent>())
        {
            rendered.AppendLine();
            rendered.Append(CallMarker).Append("name=").Append(call.Name)
                .Append(" id=").Append(call.CallId).AppendLine(MarkerEnd);
            rendered.Append(Serialize(call.Arguments));
        }
        return rendered.ToString();
    }

    /// <summary>The recorded answer as the response the run received.</summary>
    public static ChatResponse Parse(string recorded)
    {
        var (text, calls) = Split(recorded);
        List<AIContent> contents = [.. calls];
        if (contents.Count == 0 || text.Length > 0) contents.Insert(0, new TextContent(text));
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents));
    }

    private static (string Text, List<FunctionCallContent> Calls) Split(string recorded)
    {
        var lines = recorded.Split('\n');
        var text = new StringBuilder();
        var calls = new List<FunctionCallContent>();
        var arguments = new StringBuilder();
        (string Name, string Id)? open = null;
        foreach (var line in lines)
        {
            var header = ParseHeader(line);
            if (header is null)
            {
                (open is null ? text : arguments).AppendLine(line.TrimEnd('\r'));
                continue;
            }
            Close(open, arguments, calls);
            open = header;
        }
        Close(open, arguments, calls);
        return (text.ToString().TrimEnd('\n', '\r'), calls);
    }

    private static void Close(
        (string Name, string Id)? open, StringBuilder arguments, List<FunctionCallContent> calls)
    {
        if (open is null) return;
        calls.Add(new FunctionCallContent(
            open.Value.Id, open.Value.Name, Deserialize(arguments.ToString())));
        arguments.Clear();
    }

    private static (string Name, string Id)? ParseHeader(string line)
    {
        var trimmed = line.TrimEnd('\r');
        if (!trimmed.StartsWith(CallMarker, StringComparison.Ordinal)
            || !trimmed.EndsWith(MarkerEnd, StringComparison.Ordinal)) return null;
        var body = trimmed[CallMarker.Length..^MarkerEnd.Length];
        var idAt = body.IndexOf(" id=", StringComparison.Ordinal);
        if (!body.StartsWith("name=", StringComparison.Ordinal) || idAt < 0) return null;
        return (body[5..idAt], body[(idAt + 4)..]);
    }

    private static string Serialize(IDictionary<string, object?>? arguments)
    {
        try { return JsonSerializer.Serialize(arguments ?? new Dictionary<string, object?>()); }
        catch (NotSupportedException) { return "{}"; }
    }

    private static Dictionary<string, object?> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
