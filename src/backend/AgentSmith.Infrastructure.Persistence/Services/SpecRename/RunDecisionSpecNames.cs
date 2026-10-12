using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>2026-10-12-967ab: RunDecisions — SpecId filled from PhaseId where nothing has yet.</summary>
public sealed class RunDecisionSpecNames : IStoredSpecNameSurface
{
    public string Name => "RunDecisions";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        db.RunDecisions.Where(d => d.SpecId == null && d.PhaseId != null)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.SpecId, d => d.PhaseId), ct);
}
