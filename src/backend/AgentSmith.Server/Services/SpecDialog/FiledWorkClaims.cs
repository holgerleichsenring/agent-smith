using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-22-9519: what holds a claim on one filed ticket, said in the words a person reads.
/// <para>
/// ASKED TWICE, BECAUSE ONE ANSWER ARRIVES BEFORE THE OTHER. A claim takes the LEASE and then
/// enqueues; the run ROW appears only once a worker has dequeued it. Between those two moments the
/// ticket is irrevocably claimed and invisible to any row reader — and a withdrawal granted there
/// would let that run write its own terminal status over the closed one at the end, leaving a
/// record that says withdrawn about delivered work. A held lease never means "finished" either:
/// the terminal ticket write precedes the lease release.
/// </para>
/// <para>
/// The reach is every project sharing the filing project's TRACKER, the same set the filed-work
/// read uses: one ticket may route to several projects and a run is spawned per match, so looking
/// only in the filing project would miss the run that actually took it.
/// </para>
/// </summary>
internal static class FiledWorkClaims
{
    /// <summary>What holds the ticket, or null when nothing does.</summary>
    internal static async Task<string?> OnAsync(
        IServiceScope scope, IReadOnlyList<FiledWorkProject> projects, string ticketId, CancellationToken ct)
    {
        if (projects.Count == 0) return null;
        var leases = scope.ServiceProvider.GetRequiredService<ActiveRunRepository>();
        foreach (var project in projects)
        {
            var lease = await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct);
            // A lease whose run id is still null is the exact window this question exists for: the
            // claim is taken and the run row it will mint does not exist yet.
            if (lease is not null)
                return lease.RunId is { } claiming
                    ? $"is claimed by run {claiming}, which may not have started working yet."
                    : "is claimed by a run that has not started working yet.";
        }
        var names = projects.Select(p => p.Name).ToList();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var run = await uow.Set<Run>().AsNoTracking()
            .Where(r => names.Contains(r.Project) && r.TicketId == ticketId)
            .OrderByDescending(r => r.Id)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);
        return run is null ? null : $"has already been worked by run {run}.";
    }
}
