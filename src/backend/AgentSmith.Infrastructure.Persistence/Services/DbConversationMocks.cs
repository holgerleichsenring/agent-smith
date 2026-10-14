using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-8762r: both mock ports for the singleton tool and the per-message view. It opens a
/// scope per call and delegates to the scoped writer or reader repository, like <see cref="DbReferenceNotes"/>.
/// </summary>
public sealed class DbConversationMocks(IServiceScopeFactory scopeFactory) : IConversationMocks, IConversationMockReader
{
    public Task<(MockVersion? Version, string? Refusal)> AddAsync(
        string sessionId, string specId, string html, CancellationToken cancellationToken) =>
        InScope(r => r.AddAsync(sessionId, specId, html, cancellationToken));

    public Task<bool> SetShotAsync(string sessionId, string setId, byte[] jpeg, CancellationToken cancellationToken) =>
        InScope(r => r.SetShotAsync(sessionId, setId, jpeg, cancellationToken));

    public Task<IReadOnlyList<MockVersion>> ListAsync(string sessionId, CancellationToken cancellationToken) =>
        Reading(r => r.ListAsync(sessionId, cancellationToken));

    public Task<MockPage?> HtmlAsync(string sessionId, string specId, int? version, CancellationToken cancellationToken) =>
        Reading(r => r.HtmlAsync(sessionId, specId, version, cancellationToken));

    public Task<byte[]?> ShotAsync(string sessionId, string specId, int version, CancellationToken cancellationToken) =>
        Reading(r => r.ShotAsync(sessionId, specId, version, cancellationToken));

    private Task<T> InScope<T>(Func<ConversationMockRepository, Task<T>> act) => With(act);

    private Task<T> Reading<T>(Func<ConversationMockReadRepository, Task<T>> act) => With(act);

    private async Task<T> With<TRepository, T>(Func<TRepository, Task<T>> act) where TRepository : notnull
    {
        using var scope = scopeFactory.CreateScope();
        return await act(scope.ServiceProvider.GetRequiredService<TRepository>());
    }
}
