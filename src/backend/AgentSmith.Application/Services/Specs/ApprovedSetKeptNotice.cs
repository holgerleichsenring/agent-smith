using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79b: reports, ONCE per run, that an input arrived for an approved set and the set
/// was kept — as a comment on the ticket and as a decision on the run.
/// <para>
/// A run may be spawned in its own container and has a ticket provider, not a dialog connection,
/// so the ticket is where the person who commented or edited will look. The run decision puts the
/// same sentence in the run view; 2026-09-17-042ej is what shows a conversation which run is
/// working its ticket.
/// </para>
/// <para>
/// It runs BEFORE the set is published and ANSWERS whether the ticket was told, because the
/// fingerprint that clears a kept edit is written by that publish. Reporting first makes the
/// notice at-least-once: a publish that fails afterwards leaves the old fingerprint on the branch
/// and the next run says it again. That is the direction to fail in — a repeated notice is noise,
/// while clearing the cause for a notice nobody received loses the edit for good.
/// </para>
/// </summary>
public sealed class ApprovedSetKeptNotice(
    ITicketProviderFactory ticketFactory,
    IDecisionLogger decisions,
    ILogger<ApprovedSetKeptNotice> logger)
{
    /// <summary>True when the ticket was actually told, which is what lets the input be cleared.</summary>
    public async Task<bool> PostAsync(
        PipelineContext pipeline, TrackerConnection? tracker, SpecSet set, string cause,
        string? discarded, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(set);
        var body = ApprovedSetKept.Notice(
            set, cause, discarded, SpecRevisionCause.IsCommented(pipeline));
        if (body is null) return false;
        // The run's own record of it, whether or not a tracker is reachable.
        await decisions.LogAsync(null, DecisionCategory.Implementation, body, ct, "spec-set");
        if (tracker is null) return false;
        if (!pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null) return false;
        // An inline ticket exists only on this run — there is nothing to comment on.
        if (pipeline.Has(ContextKeys.InlineTicket)) return false;

        try
        {
            await ticketFactory.Create(tracker).UpdateStatusAsync(ticket.Id, body, ct);
            logger.LogInformation(
                "Told ticket {Ticket} that the approved set {Key} was kept ({Cause})",
                ticket.Id.Value, set.Key, cause);
            return true;
        }
        catch (Exception ex)
        {
            // The set is on the branch and in the run's decisions either way; a tracker that
            // refuses a comment must not end a run that is otherwise fine. It DOES leave the
            // input uncleared, so the next run reports it again.
            logger.LogWarning(ex, "Could not post the kept-set notice to ticket {Ticket}", ticket.Id.Value);
            return false;
        }
    }
}
