using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51e: reads the approved set of the ticket a conversation belongs to and computes
/// where the ticket's own text and that set disagree — before the turn runs, so the turn is handed
/// a fact instead of an instruction to find one.
/// <para>
/// Beside the ticket-text reader rather than inside it: that class answers what the ticket says,
/// this one what somebody approved for it, and they reach two different stores.
/// </para>
/// </summary>
public sealed class ApprovedSetDivergence(
    ApprovedPhaseSetRecorder approvals,
    ILogger<ApprovedSetDivergence> logger)
{
    /// <summary>
    /// The difference, or null when there is no approved set, no project to key it by, or the two
    /// already say the same thing — the case a rendered section would be noise in.
    /// </summary>
    public async Task<SetDivergence?> ForAsync(
        ResolvedProject? project, string ticketId, string ticketText, CancellationToken ct)
    {
        if (project is null || string.IsNullOrWhiteSpace(ticketId)) return null;
        try
        {
            var record = await approvals.LoadAsync(project, ticketId, ct);
            return record is null
                ? null
                : TicketSetDivergence.Between(
                    ticketText, [.. record.Set.Phases.Select(phase => phase.Draft.Goal)]);
        }
        // A store this turn could not reach says nothing about whether the two agree, and a turn
        // must still run: the conversation is the surface the operator is standing on.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Could not read the approved set of ticket {Ticket}", ticketId);
            return null;
        }
    }
}
