using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>
/// 2026-11-23-31dcg: a person's turn in a ticket's conversation answers the edits of its text that were put to it — each
/// open edit's ledger entry gets its answered mark (<see cref="TicketTextEditNotice.AnsweredPrefix"/>), so the plan hold
/// it raised clears. The conversation names its ticket by (tracker, key); the ledger by (project, ticket id), so the
/// project's ledger rows that hold an edit are matched by the key their ticket id spells. It never throws: the turn that
/// answers is the person's message, and a ledger fault must not keep it from the run it answers.
/// </summary>
public sealed class TicketTextEditAnswers(IServiceScopeFactory scopes, TicketProjects projects, ILogger<TicketTextEditAnswers> logger)
{
    public async Task AnsweredAsync(string project, string? tracker, string? ticketKey, CancellationToken ct)
    {
        if (ticketKey is null || tracker is null) return;
        try
        {
            await MarkAsync(project, tracker, ticketKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The text edits of {Ticket} in {Project} could not be marked answered", ticketKey, project);
        }
    }

    private async Task MarkAsync(string project, string tracker, string ticketKey, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved || resolved.Tracker.Name != tracker) return;
        var type = resolved.Tracker.Type.ToString().ToLowerInvariant();
        using var scope = scopes.CreateScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ReworkLedgerRepository>();
        foreach (var (_, ticketId, keys) in await ledger.WithKeyPrefixAsync(TicketTextEditNotice.KeyPrefix, [project], null, ct))
        {
            if (TicketKey.For(type, ticketId).Value != ticketKey) continue;
            foreach (var fingerprint in TicketTextEditNotice.Open(keys))
                await ledger.TryMarkSpokenAsync(project, ticketId, TicketTextEditNotice.AnsweredPrefix + fingerprint, ct);
        }
    }
}
