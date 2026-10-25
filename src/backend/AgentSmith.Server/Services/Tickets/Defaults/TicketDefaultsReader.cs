using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Conversation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets.Defaults;

/// <summary>
/// 2026-10-13-8762p: a ticket's defaults in one query — the rows of its runs and of the design conversation bound
/// to it — numbered by attempt as the other ticket reads number them. A design turn's run names no ticket, so its
/// rows reach the ticket only through the bound conversation. 2026-10-25-9324e: a memory-proposal row of an older run
/// is not served — no memory waits for a person; its standing is the runs' to decide.
/// </summary>
public sealed class TicketDefaultsReader(
    TicketRunsQuery runQuery, TicketAttempts attempts, TicketProjects projects,
    TicketTranscriptReader transcripts, IServiceScopeFactory scopes)
{
    /// <summary>Null for an unknown project, or a pair with neither a run nor a bound conversation.</summary>
    public async Task<TicketDefaultsView?> ReadAsync(
        string project, string ticketId, bool mayReadTranscript, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return null;
        var runs = await runQuery.ForTicketAsync(project, ticketId, ct);
        var conversation = (await transcripts.SessionAsync(resolved, ticketId, ct))?.SessionId;
        if (runs.Count == 0 && conversation is null) return null;
        var numbers = attempts.Of(runs).Select((row, i) => (row.Run.Id, Number: i + 1))
            .ToDictionary(a => a.Id, a => a.Number, StringComparer.Ordinal);
        var byId = runs.ToDictionary(r => r.Run.Id, r => r.Run, StringComparer.Ordinal);
        var entries = (await RowsAsync([.. byId.Keys], conversation, ct))
            .Select(row => View(row, numbers, mayReadTranscript))
            .OrderBy(Rank).ThenByDescending(e => e.TakenAt).ThenByDescending(e => e.Id).ToList();
        return new TicketDefaultsView(entries.Count(IsPending), entries.Count(e => IsPending(e) && e.HoldsSeries), entries);
    }

    private async Task<List<RunDefaultDecision>> RowsAsync(IReadOnlyList<string> runIds, string? conversation, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunDefaultDecision>().AsNoTracking()
            .Where(d => d.Kind != DefaultDecisionKinds.MemoryProposal
                && (runIds.Contains(d.RunId) || (conversation != null && d.Conversation == conversation)))
            .ToListAsync(ct);
    }

    private static TicketDefaultView View(
        RunDefaultDecision row, IReadOnlyDictionary<string, int> numbers, bool mayRead)
    {
        var hidden = row.Conversation is not null && !mayRead;
        return new TicketDefaultView(row.Id, row.RunId,
            row.Conversation is null && numbers.TryGetValue(row.RunId, out var n) ? n : null, row.Conversation, row.SpecId,
            hidden ? null : row.Question, hidden ? null : row.Chosen,
            hidden ? null : RunStoryJson.TryDeserialize<List<string>>(row.AlternativesJson) ?? [], hidden ? null : row.Why,
            row.HoldsSeries, row.Kind, row.PointerPath, row.TakenAt, row.Verdict, row.VerdictBy, row.VerdictAt, row.VerdictNote,
            Landed: null);
    }

    private static bool IsPending(TicketDefaultView entry) => entry.Verdict == DefaultDecisionVerdicts.Pending;

    private static int Rank(TicketDefaultView entry) => IsPending(entry) ? entry.HoldsSeries ? 0 : 1 : 2;
}
