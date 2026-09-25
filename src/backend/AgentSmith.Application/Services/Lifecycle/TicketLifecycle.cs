using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Post-PR ticket finalization shared by CommitAndPRHandler and InitCommitHandler:
/// transition to a configured done-status (or close as a fallback) and post a
/// PR-link summary comment. Operates on TicketId — does not require a fetched
/// Ticket — so handlers that only have the id (init-project's label-triggered
/// path) can call it without an extra FetchTicket step.
/// </summary>
/// <remarks>
/// Delegates to <see cref="ITicketProvider.FinalizeAsync"/> so the provider can
/// pick the atomic-vs-sequential shape that fits its backend. AzDO collapses
/// both writes into one PATCH to avoid the TF26071 (System.Rev) race that bit
/// production when comment + state landed as two PATCHes; GitHub/GitLab/Jira
/// stay on two sequential calls (no rev guard exists there).
/// </remarks>
/// <remarks>
/// 2026-09-25-c1f7: finalizing is also where the ticket's approved record is marked SATISFIED —
/// this is the one place both success paths (CommitAndPR and InitCommit) move a ticket into its
/// done status, so it is where "a run finished this" is known. Without it the record stays
/// outstanding forever and every later discovery query keeps naming the ticket, which is the
/// monotonic growth the parking statuses exist to prevent.
/// <para>
/// Both collaborators are OPTIONAL. The store is injected in the server; a CLI run and the
/// handler tests construct this class with neither, and a process that never reads an approved
/// record has none to satisfy.
/// </para>
/// </remarks>
public sealed class TicketLifecycle(ISpecApprovalStore? approvals = null, TimeProvider? time = null)
{
    public async Task FinalizeAsync(
        ITicketProviderFactory ticketFactory,
        TrackerConnection ticketConfig,
        TicketId ticketId,
        string? doneStatus,
        string summary,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = ticketFactory.Create(ticketConfig);
            await provider.FinalizeAsync(ticketId, summary, doneStatus, cancellationToken);
            logger.LogInformation(
                "Ticket {Ticket} finalized (status='{Status}')",
                ticketId, doneStatus ?? "<close>");
            await MarkApprovalSatisfiedAsync(ticketConfig, ticketId, logger, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to finalize ticket {Ticket}, PR was still created", ticketId);
        }
    }

    /// <summary>
    /// 2026-09-25-c1f7: stop the poll's approved-ticket clause from naming a ticket a run has
    /// finished. It never fails the finalization: the ticket IS in its done status by now, and the
    /// parking statuses exclude it on every tracker that filters by status anyway — an unmarked
    /// record costs a wider query, not a wrong one.
    /// </summary>
    private async Task MarkApprovalSatisfiedAsync(
        TrackerConnection ticketConfig, TicketId ticketId, ILogger logger, CancellationToken ct)
    {
        if (approvals is null) return;
        var key = SpecSetKey.For(ticketConfig.Type.ToString().ToLowerInvariant(), ticketId.Value);
        try
        {
            await approvals.MarkSatisfiedAsync(
                ticketConfig.Name, key.Value, (time ?? TimeProvider.System).GetUtcNow(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "The approved record {Key} could not be marked satisfied; the poll will keep "
                + "naming this ticket until it is", key.Value);
        }
    }
}
