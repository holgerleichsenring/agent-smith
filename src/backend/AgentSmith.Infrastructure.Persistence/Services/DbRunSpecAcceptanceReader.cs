using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-18-550ab: the RunSpec row's acceptance — the last one RunSpecProjection kept for the spec.</summary>
public sealed class DbRunSpecAcceptanceReader(IServiceScopeFactory scopeFactory) : IRunSpecAcceptanceReader
{
    public async Task<AcceptanceView?> ReadAsync(string runId, string specId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var json = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunSpec>().AsNoTracking()
            .Where(s => s.RunId == runId && s.SpecId == specId)
            .Select(s => s.AcceptanceJson).FirstOrDefaultAsync(cancellationToken);
        return RunStoryJson.TryDeserialize<AcceptanceView>(json);
    }
}
