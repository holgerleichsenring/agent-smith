using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// Upserts + reads run artifacts (the durable markdown slots) by (RunId, Kind)
/// over a SCOPED unit of work.
/// </summary>
public sealed class RunArtifactRepository(IUnitOfWork unitOfWork)
{
    public async Task UpsertAsync(string runId, string kind, string content, CancellationToken ct)
    {
        var row = await unitOfWork.Set<RunArtifact>().FirstOrDefaultAsync(a => a.RunId == runId && a.Kind == kind, ct);
        if (row is null) unitOfWork.Add(new RunArtifact { RunId = runId, Kind = kind, Content = content });
        else row.Content = content;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<string?> ReadAsync(string runId, string kind, CancellationToken ct) =>
        unitOfWork.Set<RunArtifact>().AsNoTracking()
            .Where(a => a.RunId == runId && a.Kind == kind)
            .Select(a => a.Content)
            .FirstOrDefaultAsync(ct);
}
