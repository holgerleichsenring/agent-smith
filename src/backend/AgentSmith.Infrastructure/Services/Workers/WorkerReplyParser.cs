using System.Text.Json;
using AgentSmith.Contracts.Models.Workers;
using static AgentSmith.Infrastructure.Services.Workers.JsonObjectScanner;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: turns the worker's stdout into a <see cref="WorkerReply"/>.
/// <para>
/// An ENVELOPE means the worker is acting: a JSON object carrying tool_calls or an
/// error. Anything else is the worker ANSWERING, and then its whole output is the
/// reply text — which is what a provider model returns too, prose and fences
/// included, and what the pipeline's tolerant parsers already expect downstream.
/// </para>
/// <para>
/// p0416 first run (2026-08-14, run ba2e step 12): the parser demanded one JSON
/// object and nothing else, and the CLI emitted an empty envelope followed by the
/// real answer as prose — so a correct answer failed the run. An agent narrates;
/// a contract that forbids narration is a contract against the nature of the
/// thing it binds. Only a genuinely empty output is still a failure.
/// </para>
/// </summary>
public sealed class WorkerReplyParser(WorkerJsonFormat json)
{
    public bool TryParse(string? stdout, out WorkerReply reply, out string? problem)
    {
        reply = new WorkerReply();
        var raw = (stdout ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            problem = "the worker returned an empty answer";
            return false;
        }

        // An envelope that ACTS wins wherever it sits, even framed by narration.
        if (TryReadEnvelope(raw, out var acting, IsActing))
        {
            reply = acting;
            problem = null;
            return true;
        }

        // A worker that answers IN the envelope is honoured too — but only when the
        // envelope is the whole output. Once there is substance outside it, the
        // envelope was narration and the substance is the answer.
        var unfenced = Unfence(raw);
        if (TryReadEnvelope(unfenced, out var answering, _ => true) && IsWholeOutput(unfenced))
        {
            reply = answering;
            problem = null;
            return true;
        }

        reply = new WorkerReply { Text = raw };
        problem = null;
        return true;
    }

    /// <summary>
    /// Acting means the worker asked for something to happen: tool calls, or an
    /// explicit refusal. An envelope with neither is indistinguishable from silence.
    /// </summary>
    private static bool IsActing(WorkerReply reply) =>
        reply.ToolCalls is { Count: > 0 } || !string.IsNullOrWhiteSpace(reply.Error);

    /// <summary>
    /// The output is nothing but this envelope, whitespace and a fence aside — and it
    /// carries envelope fields rather than a structured answer of its own.
    /// </summary>
    private static bool IsWholeOutput(string unfenced)
    {
        var text = unfenced.Trim();
        if (!text.StartsWith('{') || !text.EndsWith('}')) return false;
        if (BalancedObjects(text).FirstOrDefault()?.Length != text.Length) return false;
        return HasEnvelopeField(text);
    }

    private bool TryReadEnvelope(
        string raw, out WorkerReply envelope, Func<WorkerReply, bool> accept)
    {
        envelope = new WorkerReply();
        foreach (var candidate in BalancedObjects(Unfence(raw)))
        {
            try
            {
                if (!HasEnvelopeField(candidate)) continue;
                var parsed = JsonSerializer.Deserialize<WorkerReply>(candidate, json.Options);
                if (parsed is not null && accept(parsed))
                {
                    envelope = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Not this object — a tool's own JSON payload can appear in the text.
            }
        }
        return false;
    }
}
