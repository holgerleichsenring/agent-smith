using System.Runtime.CompilerServices;
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
/// </summary>
public sealed class CopilotSessionChatClient : IChatClient
{
    private readonly CopilotSessionRequest _template;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly CopilotSessionLease _lease;

    public CopilotSessionChatClient(
        ICopilotRuntime runtime, CopilotSessionRequest template, ILogger<CopilotSessionChatClient> logger)
    {
        _template = template;
        _lease = new CopilotSessionLease(runtime, template, logger);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var turn = await RunTurnAsync(messages, options, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, turn.Text))
        {
            ConversationId = turn.SessionId,
            ModelId = _template.Model,
            Usage = CopilotUsageMapping.ToUsageDetails(turn.Usage),
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Streaming is a session-CREATION flag, so the deltas are there either way.
        var turn = await RunTurnAsync(messages, options, cancellationToken);
        foreach (var delta in turn.Deltas)
            yield return new ChatResponseUpdate(ChatRole.Assistant, delta) { ConversationId = turn.SessionId };

        yield return new ChatResponseUpdate(ChatRole.Assistant, string.Empty)
        {
            ConversationId = turn.SessionId,
            ModelId = _template.Model,
        };
    }

    private async Task<TurnResult> RunTurnAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        // Refused, not silently degraded: letting the session own the tool loop would take one
        // rate-limit slot for a whole turn, emit one LlmCall pair for many model calls and
        // collapse the run trace to a single entry. 2026-09-23-4722a returns the loop to us.
        if (options?.Tools is { Count: > 0 })
            throw new NotSupportedException(
                "A copilot agent cannot serve a tool-bearing task yet: the session would own the "
                + "tool loop, and per-call rate limiting, cost events and the run trace would "
                + "collapse to one entry per turn. Phase 2026-09-23-4722a returns the loop to "
                + "agent-smith; until it ships, route tool-bearing roles to another provider.");

        var history = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var digests = history.Select(CopilotHistoryWatermark.Digest).ToList();
            var sent = _lease.SentCount;
            var session = await _lease.AcquireForAsync(history, digests, cancellationToken);
            if (_lease.SentCount == 0) sent = 0; // the lease rebuilt: everything is new again

            var tail = history.Skip(sent).Where(m => m.Role != ChatRole.System).ToList();
            var prompt = CopilotPromptRenderer.Render(tail);
            if (prompt.Length == 0)
                prompt = CopilotPromptRenderer.ContinuationPrompt;

            var before = await session.ReadUsageAsync(cancellationToken);
            var collector = new CopilotTurnCollector();
            using (session.Subscribe(collector.Observe))
            {
                await session.SendAsync(prompt, cancellationToken);
                await collector.WaitForIdleAsync(cancellationToken);
            }

            var after = await session.ReadUsageAsync(cancellationToken);
            _lease.Commit(digests);

            return new TurnResult(session.SessionId, collector.Text, collector.Deltas, after.Since(before));
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

    private sealed record TurnResult(string SessionId, string Text, IReadOnlyList<string> Deltas, CopilotUsage Usage);

}
