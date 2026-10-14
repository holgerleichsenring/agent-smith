using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-13-9a59o: what other members recorded about a ticket's runs — each attempt's cause, each
/// spec's acceptance judgement, and the ticket's pull request records — read, never recomputed.
/// Set-based queries over the run ids — how each run ended through <see cref="TicketRunEndingsReader"/> —
/// and the pull requests through the review record's own
/// port, which covers every record of the ticket rather than only the runs the answer lists.
/// </summary>
public sealed class TicketRunsFacts(IServiceScopeFactory scopes, TicketRunEndingsReader endings)
{
    public async Task<TicketRunsFactsRead> ReadAsync(
        string project, string ticketId, IReadOnlyList<string> runIds, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var ids = runIds.ToList();
        var attempts = await uow.Set<RunAttempt>().AsNoTracking()
            .Where(a => ids.Contains(a.RunId) && a.CauseChannel != null)
            .Select(a => new { a.RunId, a.CauseChannel, a.CauseActor, a.CauseAt, a.SourceKind })
            .ToListAsync(ct);
        var judged = await uow.Set<RunSpec>().AsNoTracking()
            .Where(s => ids.Contains(s.RunId) && s.AcceptanceJson != null)
            .Select(s => new { s.RunId, s.SpecId, s.AcceptanceJson })
            .ToListAsync(ct);
        var ended = await endings.ReadAsync(uow, ids, ct);
        var records = await scope.ServiceProvider.GetRequiredService<IPrReviewRecordReader>()
            .ByTicketAsync(project, ticketId, ct);
        return new TicketRunsFactsRead(
            attempts.ToDictionary(
                a => a.RunId, a => new TicketRunCause(a.CauseChannel!, a.CauseActor, a.CauseAt, a.SourceKind),
                StringComparer.Ordinal),
            judged.GroupBy(s => (s.RunId, s.SpecId)).ToDictionary(g => g.Key, g => g.First().AcceptanceJson!),
            TicketRunsReviews.Of(records), ended);
    }
}

/// <summary>What <see cref="TicketRunsFacts"/> read, keyed for the compose.</summary>
public sealed record TicketRunsFactsRead(
    IReadOnlyDictionary<string, TicketRunCause> Causes,
    IReadOnlyDictionary<(string RunId, string SpecId), string> Acceptance,
    IReadOnlyList<TicketPullRequestReview> Reviews,
    IReadOnlyDictionary<string, TicketRunEnding> Endings)
{
    public TicketRunEnding Ending(string runId) => Endings.GetValueOrDefault(runId) ?? TicketRunEnding.None;

    public TicketRunSpec Spec(string runId, string specId, string status, string? verdict) =>
        TicketRunsAcceptance.Of(specId, status, verdict, Acceptance.GetValueOrDefault((runId, specId)));
}
