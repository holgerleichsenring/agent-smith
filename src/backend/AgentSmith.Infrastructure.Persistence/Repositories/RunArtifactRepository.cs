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

    /// <summary>
    /// p0427: every artifact of a run whose kind starts with <paramref name="prefix"/> —
    /// the trace of a recorded run is one row per call, so reading it back is a range read.
    /// </summary>
    public async Task<IReadOnlyList<(string Kind, string Content)>> ListAsync(
        string runId, string prefix, CancellationToken ct)
    {
        var rows = await unitOfWork.Set<RunArtifact>().AsNoTracking()
            .Where(a => a.RunId == runId && a.Kind.StartsWith(prefix))
            .OrderBy(a => a.Kind)
            .Select(a => new { a.Kind, a.Content })
            .ToListAsync(ct);
        return [.. rows.Select(r => (r.Kind, r.Content ?? string.Empty))];
    }

    public Task<string?> ReadAsync(string runId, string kind, CancellationToken ct) =>
        unitOfWork.Set<RunArtifact>().AsNoTracking()
            .Where(a => a.RunId == runId && a.Kind == kind)
            .Select(a => a.Content)
            .FirstOrDefaultAsync(ct);
}
