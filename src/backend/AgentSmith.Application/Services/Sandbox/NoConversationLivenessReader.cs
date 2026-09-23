using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: the default conversation-liveness reader — no session store, so
/// no conversation is open and nothing is held. The server composition replaces it
/// with the relational reader; every other composition keeps this one and reaps
/// exactly what it reaped before the third rail existed.
/// </summary>
public sealed class NoConversationLivenessReader : IConversationLivenessReader
{
    public Task<IReadOnlyList<ConversationLiveness>> ReadAsync(
        IReadOnlyCollection<string> conversationIds, CancellationToken cancellationToken)
    {
        _ = conversationIds;
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<ConversationLiveness>>([]);
    }
}
