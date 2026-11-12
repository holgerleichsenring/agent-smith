using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-11-12-cd4ck: <see cref="IConversationNotes"/> for the singleton tool, turn and carry paths. It opens a scope
/// per call and delegates to the scoped repository, like <see cref="DbConversationMocks"/>.
/// </summary>
public sealed class DbConversationNotes(IServiceScopeFactory scopeFactory) : IConversationNotes
{
    public async Task<string?> ReadAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ConversationNotesRepository>().ReadAsync(sessionId, cancellationToken);
    }

    public async Task WriteAsync(string sessionId, string notes, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ConversationNotesRepository>().WriteAsync(sessionId, notes, cancellationToken);
    }
}
