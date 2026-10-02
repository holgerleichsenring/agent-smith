using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-02-075dd: <see cref="IReferenceNotes"/> for the singleton tool and carry paths. It opens a
/// scope per call and delegates to the scoped repository, like <see cref="DbReferenceSetReader"/>.
/// </summary>
public sealed class DbReferenceNotes(IServiceScopeFactory scopeFactory) : IReferenceNotes
{
    public async Task<IReadOnlyDictionary<string, string>> NotesAsync(string sessionId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReferenceNoteRepository>().NotesAsync(sessionId, cancellationToken);
    }

    public async Task<bool> SetAsync(string sessionId, string setId, string note, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReferenceNoteRepository>()
            .SetAsync(sessionId, setId, note, cancellationToken);
    }
}
