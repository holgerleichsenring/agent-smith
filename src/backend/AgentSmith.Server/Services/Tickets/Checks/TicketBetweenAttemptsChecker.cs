using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Checks;

/// <summary>
/// 2026-10-13-9a59h: checks a ticket between attempts. Nothing watches a ticket after a failed attempt in
/// webhook mode, so Stuck is OBSERVED here: only when the last attempt is terminal and failed, and never
/// again while its check is under five minutes old, the tracker is read once (ticket, comments, status
/// history) and the result stored; a changed result nudges the ticket's views. A live attempt reads no
/// tracker; a failed read writes nothing.
/// </summary>
public sealed class TicketBetweenAttemptsChecker(
    TicketRunsQuery runs,
    TicketAttempts attempts,
    TicketProjects projects,
    TicketTrackerObserver observer,
    TicketCheckStore store,
    IServiceScopeFactory scopes,
    ITicketActivityNudge nudge,
    TicketKeyResolver keys,
    TimeProvider clock,
    ILogger<TicketBetweenAttemptsChecker> logger)
{
    /// <summary>How long a check stands before the tracker is asked again.</summary>
    public static readonly TimeSpan ReusedFor = TimeSpan.FromMinutes(5);

    public async Task CheckAsync(string project, string ticketId, CancellationToken ct) =>
        await CheckAsync([.. (await runs.ForTicketAsync(project, ticketId, ct)).Where(r => r.Run.Pipeline == PipelinePresets.CodeName)], ct);

    /// <param name="code">The ticket's code-pipeline runs, as the detail read already holds them.</param>
    public async Task CheckAsync(IReadOnlyList<TicketRunRow> code, CancellationToken ct)
    {
        if (code.Count == 0 || attempts.Head(code).Run is not { Status: RunStatuses.Failed } last) return;
        var (project, ticketId) = (last.Project, last.TicketId);
        var now = clock.GetUtcNow();
        var held = await store.ForAsync([last.Id], ct);
        if (held.TryGetValue(last.Id, out var check) && now - check.CheckedAt < ReusedFor) return;
        if (projects.Of(project) is not { } resolved || await FingerprintAsync(last.Id, ct) is not { } fingerprint) return;
        if (await observer.ObserveAsync(resolved, last, fingerprint, ct) is not { } observation) return;
        if (!await store.WriteAsync(last.Id, now, observation, ct)) return;
        if (keys.For(project, last.Platform, ticketId) is not { } key) return;
        try
        {
            await nudge.ChangedAsync(project, key, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The changed check of ticket {Ticket} was stored but not announced", ticketId);
        }
    }

    // The fingerprint the attempt fetched; an attempt without one cannot be compared, and unknown is never unchanged.
    private async Task<string?> FingerprintAsync(string runId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunAttempt>().AsNoTracking()
            .Where(a => a.RunId == runId).Select(a => a.TicketFingerprint).FirstOrDefaultAsync(ct);
    }
}
