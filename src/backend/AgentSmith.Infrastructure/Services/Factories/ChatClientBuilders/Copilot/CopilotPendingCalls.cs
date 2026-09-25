using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-23-4722a: the tool calls a session is waiting on, keyed by the id the model used.
///
/// The model calls a tool; the runtime leaves the call pending and tells us its RequestId. We hand
/// the call out as a <see cref="FunctionCallContent"/> and end the response, so
/// FunctionInvokingChatClient runs the tool — our tool, with the approval policy, the sandbox
/// routing and the event publishing that every other provider's loop already gets. The next call
/// arrives carrying the result, and answering the pending RequestId resumes the turn.
/// </summary>
internal sealed class CopilotPendingCalls
{
    private readonly Dictionary<string, string> _requestIdByToolCallId = new(StringComparer.Ordinal);

    internal bool Any => _requestIdByToolCallId.Count > 0;

    internal void Remember(IEnumerable<CopilotSessionEvent.ExternalToolRequested> requests)
    {
        foreach (var request in requests)
            _requestIdByToolCallId[request.ToolCallId] = request.RequestId;
    }

    /// <summary>
    /// Whether this call is the continuation of a turn rather than a new one: every message it
    /// carries is a result for a call we are still waiting on. FunctionInvokingChatClient sends
    /// exactly that when the response named a ConversationId — it clears the history it was
    /// building and forwards only the new tool messages.
    /// </summary>
    internal bool IsContinuation(IReadOnlyList<ChatMessage> messages) =>
        Any
        && messages.Count > 0
        && messages.All(m => m.Role == ChatRole.Tool)
        && Results(messages).All(r => _requestIdByToolCallId.ContainsKey(r.CallId));

    /// <summary>
    /// Answers every result this call carries. A tool that threw is reported through the error
    /// argument rather than as a result, so the model is told the call failed instead of being
    /// handed an exception message as though it were an answer.
    /// </summary>
    internal async Task AnswerAsync(
        ICopilotSessionHandle session, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        foreach (var result in Results(messages))
        {
            if (!_requestIdByToolCallId.Remove(result.CallId, out var requestId)) continue;
            var (value, error) = Describe(result);
            // 2026-09-25-6b2e: the runtime says whether it took the answer. Unread, a refusal was
            // indistinguishable from the turn simply never resuming.
            if (!await session.RespondToToolAsync(requestId, value, error, cancellationToken))
                throw new InvalidOperationException(
                    $"The Copilot runtime did not accept the answer to external tool request "
                    + $"{requestId} (tool call {result.CallId}), so this turn cannot resume.");
        }
    }

    /// <summary>Forgets everything, for a session that is being replaced.</summary>
    internal void Clear() => _requestIdByToolCallId.Clear();

    private static IEnumerable<FunctionResultContent> Results(IEnumerable<ChatMessage> messages) =>
        messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>();

    private static (string? Value, string? Error) Describe(FunctionResultContent result) =>
        result.Exception is { } failure
            ? (null, failure.Message)
            : (result.Result?.ToString() ?? string.Empty, null);
}
