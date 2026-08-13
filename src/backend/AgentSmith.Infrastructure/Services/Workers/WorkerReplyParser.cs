using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: parses the worker's stdout into a <see cref="WorkerReply"/>, strictly. An
/// answer that is not the agreed JSON object is a FAILURE with a named reason — never a
/// silent degrade to "the worker said some text", which would let a broken translation
/// layer look like a model that merely chose to talk.
/// <para>
/// The single tolerated wrapper is a fenced code block: agent CLIs habitually wrap JSON
/// in ```json. Unwrapping a known envelope is not the same as guessing at prose.
/// </para>
/// </summary>
public sealed class WorkerReplyParser(WorkerJsonFormat json)
{
    private const int ExcerptLength = 300;

    public bool TryParse(string? stdout, out WorkerReply reply, out string? problem)
    {
        reply = new WorkerReply();
        var payload = Unfence(stdout ?? string.Empty);
        if (payload.Length == 0)
        {
            problem = "the worker returned an empty answer";
            return false;
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<WorkerReply>(payload, json.Options);
            if (parsed is null)
            {
                problem = $"the worker's answer parsed to null: {Excerpt(payload)}";
                return false;
            }
            reply = parsed;
            problem = null;
            return true;
        }
        catch (JsonException ex)
        {
            problem = $"the worker's answer is not the agreed JSON object ({ex.Message}): "
                + Excerpt(payload);
            return false;
        }
    }

    private static string Unfence(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        var firstBreak = text.IndexOf('\n');
        if (firstBreak < 0) return text;
        var body = text[(firstBreak + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing < 0 ? body : body[..closing]).Trim();
    }

    private static string Excerpt(string payload) =>
        payload.Length <= ExcerptLength ? payload : payload[..ExcerptLength] + " …[truncated]";
}
