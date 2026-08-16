using System.Text;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Trace;

/// <summary>
/// p0423: records what was asked and what came back, one entry each, for every provider
/// call of a traced run.
/// <para>
/// It records the messages AS SENT — the composed system prompt, the whole conversation
/// including previous tool results, the assistant's reply. That is the artefact p0422
/// needed and did not have: a fix to what the model is told could not be confirmed,
/// because nobody could read what the model was told.
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
        await trace.WriteAsync(runId, "prompt", Render(materialised), cancellationToken);
        var response = await inner.GetResponseAsync(materialised, options, cancellationToken);
        await trace.WriteAsync(runId, "answer", response.Text ?? string.Empty, cancellationToken);
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
