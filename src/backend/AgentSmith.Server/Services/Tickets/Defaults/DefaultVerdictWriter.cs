using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Defaults;

/// <summary>
/// 2026-10-13-8762p: ratifies or overrules a batch of a ticket's defaults. Every item is validated first and a
/// refusal writes nothing; each item is then one conditional update that only a pending row matches, so of two
/// people or replicas deciding one row exactly one wins and the other reads it already decided. The stored verdict
/// IS the act: an overrule — or any verdict on a row holding the series — queues one Operator rework nudge, and
/// anything applied tells the ticket page once.
/// </summary>
public sealed class DefaultVerdictWriter(
    TicketDefaultsReader reader, DefaultVerdictValidation validation, IServiceScopeFactory scopes,
    IReworkNudges nudges, ITicketActivityNudge activity, TicketKeyResolver keys, TimeProvider clock,
    ILogger<DefaultVerdictWriter> logger)
{
    public async Task<DefaultVerdictOutcome> WriteAsync(
        string project, string ticketId, IReadOnlyList<DefaultVerdictRequest>? items, string principal, CancellationToken ct)
    {
        if (await reader.ReadAsync(project, ticketId, mayReadTranscript: false, ct) is not { } listed)
            return DefaultVerdictOutcome.UnknownTicket;
        if (validation.Problems(items, listed) is { } refused) return DefaultVerdictOutcome.Invalid(refused);
        var byId = listed.Entries.ToDictionary(e => e.Id);
        var applied = new List<DefaultVerdictRequest>();
        var standing = new List<DefaultVerdictStanding>();
        using (var scope = scopes.CreateScope())
        {
            var rows = scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunDefaultDecision>();
            foreach (var item in items!)
                if (await DecideAsync(rows, item, principal, ct)) applied.Add(item);
                else standing.Add(new(item.Id, await rows.AsNoTracking().Where(r => r.Id == item.Id)
                    .Select(r => r.Verdict).FirstOrDefaultAsync(ct) ?? DefaultDecisionVerdicts.Pending));
        }
        await NudgeAsync(project, ticketId, [.. applied.Select(a => (a.Verdict, byId[a.Id].HoldsSeries))], ct);
        logger.LogInformation("Defaults of {Project}/#{Ticket} decided by {Principal}: {Applied} applied, {Standing} already decided",
            project, ticketId, principal, applied.Count, standing.Count);
        return DefaultVerdictOutcome.Done(new DefaultVerdictResult([.. applied.Select(a => a.Id)], standing));
    }

    private async Task<bool> DecideAsync(DbSet<RunDefaultDecision> rows, DefaultVerdictRequest item, string principal, CancellationToken ct)
    {
        var at = clock.GetUtcNow();
        var note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim();
        return await rows.Where(r => r.Id == item.Id && r.Verdict == DefaultDecisionVerdicts.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Verdict, item.Verdict).SetProperty(r => r.VerdictBy, principal)
                .SetProperty(r => r.VerdictAt, at).SetProperty(r => r.VerdictNote, note), ct) > 0;
    }

    private async Task NudgeAsync(string project, string ticketId, IReadOnlyList<(string Verdict, bool HoldsSeries)> applied, CancellationToken ct)
    {
        if (applied.Count == 0) return;
        var overruled = applied.Any(a => a.Verdict == DefaultDecisionVerdicts.Overruled);
        if (overruled || applied.Any(a => a.HoldsSeries))
            await nudges.EnqueueAsync(new ReworkNudgeRequest(project, ticketId, ReworkNudgeOrigin.Operator,
                Channel: overruled ? ReworkChannel.Overrule : null), ct);
        if (keys.For(project, null, ticketId) is { } key) await activity.ChangedAsync(project, key, ct);
    }
}

/// <summary>2026-10-13-8762p: what a verdict batch came to — an unknown ticket, a refusal in words, or the result.</summary>
public sealed record DefaultVerdictOutcome(bool Unknown, string? Refusal, DefaultVerdictResult? Result)
{
    public static readonly DefaultVerdictOutcome UnknownTicket = new(true, null, null);
    public static DefaultVerdictOutcome Invalid(string refusal) => new(false, refusal, null);
    public static DefaultVerdictOutcome Done(DefaultVerdictResult result) => new(false, null, result);
}
