using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-8762l: a ticket's pre-build review rounds in one read — the code attempts numbered exactly as
/// <see cref="TicketSeriesReader"/> numbers them, their <see cref="RunSpecReviewRound"/> rows in one query by
/// trail position, and the approval record's design-conversation rounds. A set approved in the conversation
/// was reviewed there and never at run time, so a ticket that never ran still has rounds to show.
/// </summary>
public sealed class TicketReviewsReader(
    TicketRunsQuery runQuery,
    TicketAttempts attempts,
    TicketProjects projects,
    ISpecApprovalStore approvals,
    IServiceScopeFactory scopes)
{
    /// <summary>Null for an unknown project, or a pair with neither a code run nor an approval record.</summary>
    public async Task<TicketReviewsView?> ReadAsync(string project, string ticketId, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return null;
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct))
            .Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        var key = TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var record = await approvals.GetAsync(resolved.Tracker.Name, key, ct);
        if (code.Count == 0 && record is null) return null;

        var ordered = attempts.Of(code).Select(a => a.Run.Id).ToList();
        var rows = await RowsAsync(ordered, ct);
        return new TicketReviewsView(project, ticketId,
            [.. ordered.Select((runId, i) => new TicketReviewAttemptView(runId, i + 1,
                TicketReviewRounds.OfRun([.. rows.Where(r => r.RunId == runId)])))],
            record is null ? null : new TicketReviewConversationView(record.Approval?.At,
                [.. (record.ReviewRounds ?? []).Select(TicketReviewRounds.OfConversation)]));
    }

    private async Task<List<RunSpecReviewRound>> RowsAsync(IReadOnlyList<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return [];
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await uow.Set<RunSpecReviewRound>().AsNoTracking()
            .Where(r => runIds.Contains(r.RunId)).OrderBy(r => r.EventSeq).ThenBy(r => r.Id).ToListAsync(ct);
    }
}
