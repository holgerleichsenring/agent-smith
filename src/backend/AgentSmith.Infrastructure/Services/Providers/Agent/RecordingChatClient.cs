using System.Text;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// p0423: records what was asked and what came back, one entry each, for every provider
/// call of a traced run.
/// <para>
/// It records the messages AS SENT — the composed system prompt, the whole conversation
/// including previous tool results, the assistant's reply. That is the artefact p0422
/// needed and did not have: a fix to what the model is told could not be confirmed,
/// because nobody could read what the model was told.
/// </para>
/// <para>
/// p0427: it sits in the FACTORY's chain, below the tool loop — so every consumer records
/// (analyzer and spec derivation included, not only the master's skill calls) and every
/// provider round-trip is its own entry. Recorded above the loop, a whole tool loop
/// collapsed into one flattened entry that no replay could reproduce; p0176b had already
/// moved the event decorator here for the same reason.
/// </para>
/// </summary>
public sealed class RecordingChatClient(
    IChatClient inner, IRunTraceWriter trace, IRunContextAccessor runContext) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materialised = messages as IList<ChatMessage> ?? messages.ToList();
        var runId = runContext.CurrentRunId ?? string.Empty;
        await trace.WriteAsync(
            runId, RecordedTrace.PromptLabel, Render(materialised), cancellationToken);
        var response = await inner.GetResponseAsync(materialised, options, cancellationToken);
        // p0427: an answer is TEXT AND CALLS. Recording only the text made every tool-calling
        // answer record as empty — the half of a run a replay has to reproduce.
        await trace.WriteAsync(
            runId, RecordedTrace.AnswerLabel, TracedAnswer.Render(response), cancellationToken);
        return response;
    }

    private static string Render(IEnumerable<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var message in messages)
        {
            sb.Append("=== ").Append(message.Role).AppendLine(" ===");
            foreach (var text in message.Contents.OfType<TextContent>())
                sb.AppendLine(text.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null)
        => inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();
}
