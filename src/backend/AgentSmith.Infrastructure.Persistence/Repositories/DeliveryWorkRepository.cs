using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-25-0099b: what the window's attempts worked on — their specs and the repositories they changed (those
/// with a pull request, 2026-10-26-cb25f). Both
/// are joined to the scoped run window, never read by a list of run ids.
/// </summary>
public sealed class DeliveryWorkRepository(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<DeliverySpecRow>> SpecsAsync(
        ProjectScope scope, string fromId, string toId, CancellationToken ct) =>
        await (from spec in unitOfWork.Set<RunSpec>().AsNoTracking()
               join run in Window(scope, fromId, toId) on spec.RunId equals run.Id
               select new DeliverySpecRow(spec.RunId, spec.Ordinal, spec.Title, spec.Status))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DeliveryRepoRow>> ReposAsync(
        ProjectScope scope, string fromId, string toId, CancellationToken ct) =>
        await (from repo in unitOfWork.Set<RunRepo>().AsNoTracking()
               join run in Window(scope, fromId, toId) on repo.RunId equals run.Id
               // 2026-10-26-cb25f: a repository the run changed carries the pull request its outcome recorded.
               where repo.PrUrl != null && repo.PrUrl != ""
               select new DeliveryRepoRow(repo.RunId, repo.RepoName))
            .ToListAsync(ct);

    private IQueryable<Run> Window(ProjectScope scope, string fromId, string toId) =>
        unitOfWork.Set<Run>().AsNoTracking().InWindow(scope, fromId, toId);
}
