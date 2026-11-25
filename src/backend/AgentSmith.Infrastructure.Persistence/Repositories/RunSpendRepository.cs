using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-24-0fbfb: every run whose id falls in [fromId, toId) in the caller's projects, of any pipeline, with its
/// cost and repositories, and the count of their llm calls. A run id begins with its UTC start, so the window is a
/// text range SQL compares on every provider (DeliveryRunWindow); the costs are summed by the caller, in memory,
/// because SQLite cannot sum decimals.
/// </summary>
public sealed class RunSpendRepository(IUnitOfWork unitOfWork)
{
    public async Task<RunSpendRead> ReadAsync(ProjectScope scope, string fromId, string toId, CancellationToken ct)
    {
        var window = Window(scope, fromId, toId);
        var runs = await window.Select(r => new { r.Id, r.Pipeline, r.CostTotalUsd }).ToListAsync(ct);
        if (runs.Count == 0) return new RunSpendRead([], 0);
        var ids = window.Select(r => r.Id);
        var repos = (await unitOfWork.Set<RunRepo>().AsNoTracking().Where(x => ids.Contains(x.RunId))
                .Select(x => new { x.RunId, x.RepoName }).ToListAsync(ct))
            .ToLookup(x => x.RunId, x => x.RepoName, StringComparer.Ordinal);
        var calls = await unitOfWork.Set<RunLlmCall>().AsNoTracking().CountAsync(c => ids.Contains(c.RunId), ct);
        return new RunSpendRead(
            [.. runs.Select(r => new RunSpendRow(r.Id, r.Pipeline, r.CostTotalUsd, [.. repos[r.Id].Distinct(StringComparer.Ordinal)]))],
            calls);
    }

    /// <summary>The window's runs in the caller's projects, as SQL filters them.</summary>
    public IQueryable<Run> Window(ProjectScope scope, string fromId, string toId) =>
        unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => string.Compare(r.Id, fromId) >= 0 && string.Compare(r.Id, toId) < 0)
            .Within(scope);
}
