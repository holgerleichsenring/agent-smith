using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51e: approving an amendment — the ticket this conversation BELONGS to is
/// rewritten from the specification just approved, and the approved set under it becomes that one.
/// <para>
/// WHAT IT BUYS IS A CORRECT HUMAN, not a correct run. A run reads the branch and nothing else;
/// on a stamped ticket the description is never consulted. A stale body therefore mis-informs
/// whoever reads the ticket, reviews the pull request or judges the result — and that is who this
/// is for. Saying more than that would be selling it.
/// </para>
/// <para>
/// THE ORDER IS THE TICKET, THEN THE RECORD, THEN THE BRANCH. A rewrite that does not land leaves
/// all three exactly as they were, which is the only state a refusal may leave behind.
/// </para>
/// <para>
/// Its own class because <see cref="OutcomeTicketFiler"/> is at the file-length limit, and
/// because this answers a different question: not what to create, but what one existing ticket
/// may be made to say.
/// </para>
/// </summary>
public sealed class TicketAmendment(
    AgentSmithConfig config,
    IServiceScopeFactory scopeFactory,
    FiledWorkTrackerProjects trackerProjects,
    Infrastructure.Persistence.Repositories.SpecDialogTicketTextRepository ticketText,
    ApprovedPhaseSetRecorder approvals,
    ITicketProviderFactory trackers,
    PhaseTicketRenderer renderer,
    EpicChildOrderer orderer,
    FiledSpecBranch branches,
    ILogger<TicketAmendment> logger)
{
    /// <summary>What the thread is told — the amendment landed, or why nothing was written.</summary>
    public async Task<string> ApplyAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        var name = state.Scope?.Project ?? state.Project;
        if (!config.Projects.TryGetValue(name, out var project))
            return $"Project '{name}' is no longer in the configuration catalog, so this "
                + "conversation's ticket cannot be reached. Nothing was changed.";
        if (await ticketText.GetAsync(state.JobId, ct) is not { TicketId: { Length: > 0 } ticketId })
            return "This conversation never recorded which ticket it belongs to, so there is "
                + "nothing to amend. Nothing was changed.";
        var amended = AmendedSpecification.Of(proposal, state.JobId, renderer, orderer);
        if (amended.Error is { } refused) return $"{refused} Nothing was changed.";

        using var scope = scopeFactory.CreateScope();
        var reach = trackerProjects.SharingTrackerWith(name);
        if (await FiledWorkClaims.OnAsync(scope, reach, ticketId, ct) is { } claimed)
            return $"Ticket {ticketId} {claimed} An amendment rewrites the specification a run "
                + "works from, so it is refused once a run has taken the ticket. Nothing was "
                + "changed — amend it when no run holds it.";
        try
        {
            return await WriteAsync(state, project, ticketId, amended, ct);
        }
        // The conversation asked for this write and has to hear how it ended; a thrown turn
        // reports a failure with no subject and leaves the operator guessing what landed.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Amending ticket {Ticket} failed", ticketId);
            return $"Ticket {ticketId} was rewritten, but the amendment did not complete: "
                + $"{ex.Message}. Check the ticket and its approved set before triggering it.";
        }
    }

    private async Task<string> WriteAsync(
        ConversationState state, ResolvedProject project, string ticketId,
        AmendedSpecification amended, CancellationToken ct)
    {
        var id = new TicketId(ticketId);
        var rewrite = await trackers.CreateRewriter(project.Tracker)
            .RewriteRegionAsync(id, amended.Region, ct);
        if (!rewrite.Rewritten)
            return $"Ticket {ticketId} was NOT rewritten, and nothing else was changed either — "
                + $"the approved set under it is the one it had. {rewrite.Reason}";

        var record = await approvals.RecordAsync(state, project, ticketId, amended.Set, ct);
        // Read back AFTER the rewrite: the fingerprint the branch set carries is of the ticket as
        // the TRACKER stored it, and a stale one reads as a ticket edit on the next run — which
        // posts a notice telling the operator their edit was ignored, after every amendment, for
        // ever. The read-back is also what proves the rewrite is on the ticket.
        var branch = await branches.WriteAsync(
            project, record, id, await ReadBackAsync(project, id, ct), carrier: null, ct);
        logger.LogInformation(
            "Ticket {Ticket} was amended from conversation {Session}: {Phases} phase(s), branch {State}",
            ticketId, state.JobId, amended.Set.Count, branch.Written ? "written" : branch.Error);
        return branch.Written
            ? $"Ticket {ticketId} now says what this specification says, and the approved set "
                + $"under it is the one you just approved ({amended.Set.Count} phase(s))."
            : $"Ticket {ticketId} now says what this specification says and the approved set "
                + $"under it is the one you just approved, but the set is NOT on the ticket "
                + $"branch ({branch.Error}). A run that claims this ticket works the set the "
                + "branch still carries — amend again once the branch can be written.";
    }

    private async Task<Ticket?> ReadBackAsync(ResolvedProject project, TicketId id, CancellationToken ct)
    {
        try { return await trackers.Create(project.Tracker).GetTicketAsync(id, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The amended ticket {Ticket} could not be read back", id.Value);
            return null;
        }
    }
}
