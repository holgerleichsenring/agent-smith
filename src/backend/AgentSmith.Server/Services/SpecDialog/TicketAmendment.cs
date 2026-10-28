using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51e: an approval in the conversation of a ticket no code attempt has worked — the
/// approved series becomes the one under the ticket, and the ticket's description says what it says.
/// <para>
/// 2026-10-13-9a59l: THE RECORD AND THE BRANCH ARE WHAT A RUN READS, so they are written on every
/// tracker. The description rewrite is a courtesy for whoever reads the ticket: attempted where the
/// tracker supports it, said when refused or failed, never blocking. It still goes FIRST, so the
/// branch set's fingerprint is the text the tracker stored. A ticket a claim holds is refused whole.
/// </para>
/// </summary>
public sealed class TicketAmendment(
    AgentSmithConfig config,
    IServiceScopeFactory scopeFactory,
    FiledWorkTrackerProjects trackerProjects,
    Infrastructure.Persistence.Repositories.SpecDialogTicketTextRepository ticketText,
    ApprovedSpecSetRecorder approvals,
    ITicketProviderFactory trackers,
    SpecTicketRenderer renderer, EpicChildOrderer orderer, FiledSeriesFactory seriesFiling,
    FiledSpecBranch branches, TicketDescriptionRewrite descriptions,
    ILogger<TicketAmendment> logger)
{
    /// <summary>What the thread is told, and whether the approved series was recorded.</summary>
    /// <param name="continuation">The record's continuation — null for a plain never-worked hand-off.</param>
    public async Task<TicketAmendmentResult> ApplyAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken ct, SeriesContinuation? continuation = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var name = state.Scope?.Project ?? state.Project;
        if (!config.Projects.TryGetValue(name, out var project))
            return new($"Project '{name}' is no longer in the configuration catalog, so this "
                + "conversation's ticket cannot be reached. The approval was not recorded.", false);
        if (await ticketText.GetAsync(state.JobId, ct) is not { TicketId: { Length: > 0 } ticketId })
            return new("This conversation never recorded which ticket it belongs to, so there is "
                + "nothing to record the approval under. The approval was not recorded.", false);
        // 2026-10-06-03c7c: the ticket keeps the series base its filing minted.
        var previous = await approvals.LoadAsync(project, ticketId, ct);
        var series = seriesFiling.KeptOrMinted(previous);
        var amended = AmendedSpecification.Of(
            proposal, series, state.JobId, new AmendmentRendering(renderer, orderer, seriesFiling),
            TicketLabelVocabulary.For(project.Tracker));
        if (amended.Error is { } refused) return new($"{refused} The approval was not recorded.", false);

        using var scope = scopeFactory.CreateScope();
        if (await FiledWorkClaims.LeaseOnAsync(scope, trackerProjects.SharingTrackerWith(name), ticketId, ct) is { } claimed)
            return new($"Ticket {ticketId} {claimed} The approval was not recorded: the series a run works "
                + "from is not replaced under a run that holds the ticket — approve again when no run holds it.", false);
        try
        {
            return new(await WriteAsync(state, project, ticketId, amended, continuation, previous, ct), true);
        }
        // The conversation asked for this write and has to hear how it ended, never a thrown turn.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Recording the approval under ticket {Ticket} failed", ticketId);
            return new($"The approval under ticket {ticketId} did not complete: {ex.Message}. Check the "
                + "ticket's approved series before triggering it, and approve again.", false);
        }
    }

    private async Task<string> WriteAsync(
        ConversationState state, ResolvedProject project, string ticketId,
        AmendedSpecification amended, SeriesContinuation? continuation, SpecApprovalRecord? previous, CancellationToken ct)
    {
        var id = new TicketId(ticketId);
        var rewrite = await descriptions.WriteAsync(project, id, amended.Region, previous, ct);
        var record = await approvals.RecordAsync(state, project, ticketId, amended.Set, amended.Goal, ct, continuation);
        // Read back AFTER the rewrite: the fingerprint the branch set carries is of the ticket as the
        // TRACKER stored it, and a stale one reads as a ticket edit on the next run. 2026-10-12-f5f7d:
        // each spec is written into the repository it names; what already ran stays out by id.
        var branch = await branches.WriteAsync(project, record, id, await ReadBackAsync(project, id, ct), ct, amended.Set.Sources);
        logger.LogInformation(
            "Approval recorded under ticket {Ticket} from conversation {Session}: {Specs} spec(s), branch {State}",
            ticketId, state.JobId, amended.Set.Drafts.Count, branch.Written ? "written" : branch.Error);
        var recorded = $"The approved series under ticket {ticketId} is the one you just approved "
            + $"({amended.Set.Drafts.Count} spec(s)). {rewrite}{string.Concat((branch.Notes ?? []).Select(n => " " + n))}";
        return branch.Written ? recorded
            : $"{recorded} The series is NOT on the ticket branch yet ({branch.Error}); a run that claims "
                + "this ticket works the set the branch still carries — approve again once it can be written.";
    }

    private async Task<Ticket?> ReadBackAsync(ResolvedProject project, TicketId id, CancellationToken ct)
    {
        try { return await trackers.Create(project.Tracker).GetTicketAsync(id, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The ticket {Ticket} could not be read back", id.Value);
            return null;
        }
    }
}
