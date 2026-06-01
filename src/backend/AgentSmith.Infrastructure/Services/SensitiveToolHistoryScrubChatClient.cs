using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// p0191: <see cref="IChatClient"/> decorator that scrubs prior-turn tool
/// results from sensitive tools out of the conversation history before each
/// request hits the LLM provider. A sensitive tool result is "prior" when at
/// least one assistant message follows it in the message list — i.e. the
/// agent has already seen and acted on it. The fresh tool result of the
/// current turn (the last tool result before the LLM is invoked again) is
/// passed through unchanged so the agent gets the credentials exactly once.
/// Stateless: relies on the message-list ordering, not per-instance state.
///
/// Adding a new sensitive tool: add the tool name to <see cref="SensitiveToolNames"/>
/// AND mark its host with <c>AgentSmith.Contracts.Services.ISensitiveToolHost</c>.
/// The two sites are co-edited or the redaction silently drops.
/// </summary>
internal sealed class SensitiveToolHistoryScrubChatClient : DelegatingChatClient
{
    private const string ScrubMarker = "[set, applied earlier turn]";

    public SensitiveToolHistoryScrubChatClient(IChatClient inner) : base(inner)
    {
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var scrubbed = Scrub(messages.ToList());
        return base.GetResponseAsync(scrubbed, options, cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var scrubbed = Scrub(messages.ToList());
        return base.GetStreamingResponseAsync(scrubbed, options, cancellationToken);
    }

    private static IList<ChatMessage> Scrub(IList<ChatMessage> messages)
    {
        var sensitiveCallIds = CollectSensitiveCallIds(messages);
        if (sensitiveCallIds.Count == 0) return messages;

        var result = new List<ChatMessage>(messages.Count);
        for (var i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            if (ShouldScrub(msg, i, messages, sensitiveCallIds))
                result.Add(ScrubToolMessage(msg));
            else
                result.Add(msg);
        }
        return result;
    }

    private static bool ShouldScrub(
        ChatMessage msg, int index, IList<ChatMessage> all, IReadOnlySet<string> sensitiveCallIds)
    {
        if (msg.Role != ChatRole.Tool) return false;
        if (!HasSensitiveCallId(msg, sensitiveCallIds)) return false;
        return IsFollowedByAssistant(all, index);
    }

    private static HashSet<string> CollectSensitiveCallIds(IList<ChatMessage> messages)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var msg in messages)
        {
            if (msg.Contents is null) continue;
            foreach (var part in msg.Contents)
            {
                if (part is FunctionCallContent call
                    && SensitiveToolNames.All.Contains(call.Name)
                    && !string.IsNullOrEmpty(call.CallId))
                {
                    ids.Add(call.CallId);
                }
            }
        }
        return ids;
    }

    private static bool HasSensitiveCallId(ChatMessage msg, IReadOnlySet<string> sensitiveCallIds)
    {
        if (msg.Contents is null) return false;
        foreach (var part in msg.Contents)
        {
            if (part is FunctionResultContent result
                && !string.IsNullOrEmpty(result.CallId)
                && sensitiveCallIds.Contains(result.CallId))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsFollowedByAssistant(IList<ChatMessage> messages, int index)
    {
        for (var j = index + 1; j < messages.Count; j++)
        {
            if (messages[j].Role == ChatRole.Assistant) return true;
        }
        return false;
    }

    private static ChatMessage ScrubToolMessage(ChatMessage original)
    {
        var scrubbedContents = new List<AIContent>();
        if (original.Contents is not null)
        {
            foreach (var part in original.Contents)
            {
                if (part is FunctionResultContent result)
                    scrubbedContents.Add(new FunctionResultContent(result.CallId, ScrubMarker));
                else
                    scrubbedContents.Add(part);
            }
        }
        return new ChatMessage(ChatRole.Tool, scrubbedContents);
    }
}
