using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-10-13-9a59h: one read of a ticket's conversation, in time order and grouped by attempt — what its
/// attempts recorded, its pull requests' records (through the read port, never their tables), the live
/// ticket thread and the bound Work it out conversation. The turns and the open proposal are left out for a
/// caller who may not read them, and the view says so explicitly.
/// </summary>
public sealed class TicketConversationReader(
    TicketRunsQuery runQuery,
    TicketAttempts attempts,
    TicketAttemptRecords attemptRecords,
    TicketProjects projects,
    ConversationRunRecords runRecords,
    TicketThreadReader thread,
    TicketTranscriptReader transcript,
    IServiceScopeFactory scopes)
{
    /// <summary>Null for an unknown project, or a ticket with no code run and no bound conversation.</summary>
    public async Task<TicketConversationView?> ReadAsync(
        string project, string ticketId, bool mayReadTranscript, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return null;
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct)).Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        var session = await transcript.SessionAsync(resolved, ticketId, ct);
        if (code.Count == 0 && session is null) return null;
        var ordered = attempts.Of(code).Select(a => a.Run).ToList();
        var runIds = ordered.Select(r => r.Id).ToList();
        var rows = await runRecords.ReadAsync(runIds, ct);
        var comments = await thread.ReadAsync(resolved, ticketId, ct);
        var hidden = session is not null && !mayReadTranscript;

        var entries = new List<TicketConversationEntryView>(ConversationRunEntries.Of(ordered, rows, comments ?? []));
        entries.AddRange(ConversationPrEntries.Of(await PrRecordsAsync(project, ticketId, ct), runIds.ToHashSet(StringComparer.Ordinal)));
        entries.AddRange(ConversationAssembly.Thread(comments ?? []));
        if (session is not null && !hidden) entries.AddRange(transcript.Turns(session));
        var lastApproval = rows.Attempts.FirstOrDefault(a => a.RunId == runIds.LastOrDefault())?.ApprovalAt;
        var placed = ConversationAssembly.Folded(entries)
            .Select(e => ConversationAssembly.Placed(e, ordered, lastApproval))
            .OrderBy(e => e.At).ToList();
        return new TicketConversationView(project, ticketId, placed, await MarkersAsync(ordered, rows, placed, ct),
            hidden ? null : session?.SessionId, hidden || session is not { IsOpen: true } ? null : session.ThreadId,
            hidden || session is null ? null : transcript.Proposal(session), hidden, comments is null);
    }

    private async Task<IReadOnlyList<TicketConversationAttemptView>> MarkersAsync(
        IReadOnlyList<Run> ordered, ConversationRunRows rows, IReadOnlyList<TicketConversationEntryView> entries, CancellationToken ct)
    {
        var records = await attemptRecords.ForAsync([.. ordered.Select(r => r.Id)], ct);
        return [.. ordered.Select((run, i) => new TicketConversationAttemptView(run.Id, i + 1,
            records.GetValueOrDefault(run.Id)?.Cause, records.GetValueOrDefault(run.Id)?.Revision,
            ConversationAssembly.Demand(rows.Attempts.FirstOrDefault(a => a.RunId == run.Id), entries)))];
    }

    private async Task<IReadOnlyList<PrReviewRecordView>> PrRecordsAsync(string project, string ticketId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPrReviewRecordReader>().ByTicketAsync(project, ticketId, ct);
    }
}
