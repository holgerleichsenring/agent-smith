namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: reads the design-conversation rows a reaper's labelled sandboxes
/// name — one query for the whole scan, never one per container. A composition with no
/// session store binds a reader that answers nothing, which is exactly today's
/// behaviour: no conversation is held, so no sandbox is spared for one.
/// </summary>
public interface IConversationLivenessReader
{
    Task<IReadOnlyList<ConversationLiveness>> ReadAsync(
        IReadOnlyCollection<string> conversationIds, CancellationToken cancellationToken);
}
