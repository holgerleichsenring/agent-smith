using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-11-26-a12ed: a run's report from the one place it is kept — the run row's AcceptanceJson.</summary>
public sealed class DbRunReportReader(IServiceScopeFactory scopes) : IRunReportReader
{
    public async Task<string?> ReportAsync(string runId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        using var scope = scopes.CreateScope();
        var json = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<Run>().AsNoTracking()
            .Where(r => r.Id == runId).Select(r => r.AcceptanceJson).FirstOrDefaultAsync(cancellationToken);
        return RunReportText.Of(RunStoryJson.TryDeserialize<AcceptanceView>(json));
    }
}
