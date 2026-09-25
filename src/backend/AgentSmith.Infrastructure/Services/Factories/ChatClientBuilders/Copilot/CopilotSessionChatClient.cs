using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: an <see cref="IChatClient"/> over ONE Copilot session.
///
/// A session is conversation state and an IChatClient call is not: every call hands us the full
/// history while the session accumulates its own, and no API seeds a foreign history into a fresh
/// session. <see cref="CopilotSessionLease"/> therefore decides, per call, whether the live
/// session can still serve what it is being handed.
///
/// One GetResponseAsync is one model call, including when tools are in play: the tools are declared
/// to the session WITHOUT bodies, so a tool call comes back to us as a
/// <see cref="FunctionCallContent"/> and the loop stays with FunctionInvokingChatClient — where the
/// iteration cap, the rate limiter, the cost events and the run trace all already live.
/// </summary>
public sealed class CopilotSessionChatClient : IChatClient
{
    private readonly CopilotSessionRequest _template;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly CopilotSessionLease _lease;
    private readonly CopilotPendingCalls _pending = new();
    private readonly CopilotTurnDriver _driver;

    public CopilotSessionChatClient(
        ICopilotRuntime runtime, CopilotSessionRequest template, ILogger<CopilotSessionChatClient> logger)
    {
        _template = template;
        _lease = new CopilotSessionLease(runtime, template, logger);
        _driver = new CopilotTurnDriver(_pending);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var turn = await RunTurnAsync(messages, options, cancellationToken);
        var message = new ChatMessage(ChatRole.Assistant, turn.Contents());
        return new ChatResponse(message)
        {
            // Naming the conversation makes FunctionInvokingChatClient send only the new tool
            // messages next time, instead of replaying an assistant message we synthesised and
            // never sent to the session.
            ConversationId = turn.SessionId,
            ModelId = _template.Model,
            FinishReason = turn.ToolRequests.Count > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
            Usage = CopilotUsageMapping.ToUsageDetails(turn.Usage),
        };
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        CopilotStreaming.UpdatesAsync(
            () => RunTurnAsync(messages, options, cancellationToken), _template.Model, cancellationToken);

    private async Task<CopilotTurnResult> RunTurnAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        var history = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var tools = CopilotToolProjection.From(options?.Tools ?? []);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // A call that carries only results for calls we are waiting on is the CONTINUATION of a
            // turn, not a new one: the session is mid-answer and wants the results, not a prompt.
            if (_pending.IsContinuation(history))
                return await _driver.RunAsync(
                    _lease.Current!, c => _pending.AnswerAsync(_lease.Current!, history, c), cancellationToken,
                    answersPendingCalls: true);

            var digests = history.Select(CopilotHistoryWatermark.Digest).ToList();
            var sent = _lease.SentCount;
            var session = await _lease.AcquireForAsync(history, digests, tools, cancellationToken);
            if (_lease.SentCount == 0)
            {
                sent = 0;              // the lease rebuilt: everything is new again, and a pending
                _pending.Clear();      // call belonged to a session that no longer exists
            }

            var tail = history.Skip(sent).Where(m => m.Role != ChatRole.System).ToList();
            var prompt = CopilotPromptRenderer.Render(tail);
            if (prompt.Length == 0) prompt = CopilotPromptRenderer.ContinuationPrompt;

            var turn = await _driver.RunAsync(session, c => session.SendAsync(prompt, c), cancellationToken);
            _lease.Commit(digests);
            return turn;
        }
        finally
        {
            _gate.Release();
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType == typeof(CopilotSessionChatClient) ? this : null;

    // The session goes away with the runtime's own shutdown; blocking here would stall the
    // chat-client cache's disposal for the length of an RPC round trip.
    public void Dispose() => _gate.Dispose();

}
