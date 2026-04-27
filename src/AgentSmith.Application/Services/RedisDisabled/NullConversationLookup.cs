using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.RedisDisabled;

/// <summary>
/// Fallback IConversationLookup for Redis-less runs. PR-comment dialogue routing is a
/// webhook-only path — manual CLI commands never invoke it. Returns null on lookup so
/// dialogue routers degrade gracefully (the existing WebhookDialogueRouter already handles
/// null lookup with a warning log).
/// </summary>
public sealed class NullConversationLookup : IConversationLookup
{
    public Task<ConversationLookupResult?> FindByPrAsync(
        string platform, string repoFullName, string prIdentifier,
        CancellationToken cancellationToken)
        => Task.FromResult<ConversationLookupResult?>(null);
}
