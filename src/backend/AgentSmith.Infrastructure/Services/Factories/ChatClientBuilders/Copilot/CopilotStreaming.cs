using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-07-d5f2: a finished turn as a stream of updates. Streaming is a session-CREATION flag
/// rather than a per-call choice, so the deltas are collected whichever overload the caller used.
/// </summary>
internal static class CopilotStreaming
{
    internal static async IAsyncEnumerable<ChatResponseUpdate> UpdatesAsync(
        Func<Task<CopilotTurnResult>> runTurn,
        string? modelId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var turn = await runTurn();
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var delta in turn.Deltas)
            yield return new ChatResponseUpdate(ChatRole.Assistant, delta) { ConversationId = turn.SessionId };

        yield return new ChatResponseUpdate(ChatRole.Assistant, turn.Contents())
        {
            ConversationId = turn.SessionId,
            ModelId = modelId,
        };
    }
}
