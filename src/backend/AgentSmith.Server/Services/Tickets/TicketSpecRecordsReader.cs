using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// The spec records a set of attempts stored — one <c>spec_record:{SpecId}</c> artifact per spec that
/// reached the record step — in one set query.
/// </summary>
public sealed class TicketSpecRecordsReader(IServiceScopeFactory scopes)
{
    public async Task<IReadOnlyList<RunArtifact>> ReadAsync(IReadOnlyList<string> runIds, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await uow.Set<RunArtifact>().AsNoTracking()
            .Where(a => runIds.Contains(a.RunId) && a.Kind.StartsWith(RunSpecProjection.RecordKindPrefix))
            .OrderBy(a => a.Id).ToListAsync(ct);
    }
}
