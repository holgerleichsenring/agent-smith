using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-09-22-2d11a: the relational conversation-liveness reader. A sandbox reaper is a
/// background singleton, so it opens a SCOPE per read and delegates to the scoped
/// session repository — the DbActiveRunLease pattern, for the same reason.
/// </summary>
public sealed class DbConversationLivenessReader(IServiceScopeFactory scopeFactory)
    : IConversationLivenessReader
{
    public async Task<IReadOnlyList<ConversationLiveness>> ReadAsync(
        IReadOnlyCollection<string> conversationIds, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sessions = await scope.ServiceProvider
            .GetRequiredService<SpecDialogSessionRepository>()
            .ListBySessionIdsAsync(conversationIds, cancellationToken);
        return [.. sessions.Select(session => new ConversationLiveness(
            session.SessionId, session.Project, session.IsOpen, session.LastActivityAt))];
    }
}
