using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315c: files a confirmed outcome into the ACTIVE SCOPE's tracker via the existing provider
/// factory. Bug → the fix-bug ticket shape (title + body, no label — the same shape the
/// create-ticket chat intent files). 2026-09-22-b3d7: a phase and an approved cut → ONE
/// `phase`-labelled ticket each, through <see cref="ApprovedSetTicketFiler"/>. The slice records
/// a cut used to file beside its work ticket were a second copy of that ticket's own slice list
/// and are gone with their filer; what a cut IS did not move — the model still decides the
/// slices, the parser still checks them, and the orderer below still refuses a cut whose edges
/// cannot be ordered, before any ticket exists.
/// </summary>
public sealed class OutcomeTicketFiler(
    AgentSmithConfig config,
    ITicketProviderFactory ticketFactory,
    PhaseTicketRenderer renderer, BugTicketRenderer bugRenderer,
    EpicChildOrderer orderer,
    ApprovedSetTicketFiler sets,
    FiledWorkStarter starter, TicketKindResolver kinds,
    ILogger<OutcomeTicketFiler> logger)
{
    public async Task<FilingReport> FileAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
        CancellationToken cancellationToken)
    {
        var filed = new List<FiledTicket>();
        try
        {
            var project = ResolveProject(state);
            var provider = ticketFactory.Create(project.Tracker);
            await (proposal switch
            {
                BugOutcome bug => FileBugAsync(
                    provider, project, bug.Ticket, filed, mayStartRuns, cancellationToken),
                PhaseOutcome phase => FilePhaseAsync(
                    provider, state, project, phase.Draft, filed, mayStartRuns, cancellationToken),
                EpicOutcome epic => FileEpicAsync(
                    provider, state, project, epic, filed, mayStartRuns, cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Outcome kind '{proposal.GetType().Name}' cannot be filed."),
            });
            return new FilingReport(filed, Error: null);
        }
        // A tracker timeout is a filing failure the report names; only the caller's escapes.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "Ticket filing failed for spec-dialog session {SessionId} after {Count} ticket(s)",
                state.JobId, filed.Count);
            return new FilingReport(filed, ex.Message);
        }
    }

    private ResolvedProject ResolveProject(ConversationState state)
    {
        var project = state.Scope?.Project ?? state.Project;
        if (string.IsNullOrWhiteSpace(project))
            throw new InvalidOperationException(
                "The spec-dialog session has no active-scope project to file tickets into.");
        return config.Projects.TryGetValue(project, out var resolved) ? resolved
            : throw new InvalidOperationException(
                $"Active-scope project '{project}' is not in the configuration catalog.");
    }

    // A bug carries NO framework label, so the project's own rules decide which pipeline claims
    // it — "started" is any match naming this project, not the phase-execution preset.
    private async Task FileBugAsync(
        ITicketProvider provider, ResolvedProject project, BugTicketDraft ticket,
        List<FiledTicket> filed, bool mayStartRuns, CancellationToken ct)
    {
        // 2026-09-15-6d9c: rendered, not composed here — the pane shows the same body first.
        var title = TicketTitle.Fit(ticket.Title);
        var created = await provider.CreateAsync(
            title, bugRenderer.RenderBody(ticket), [], kinds.For(project, TicketFilingRole.Bug), ct);
        filed.Add(FiledTicket.Of(created, title, project));
        await starter.StampAsync(provider, project, created, [], mayStartRuns, filed, ct);
    }

    private Task FilePhaseAsync(
        ITicketProvider provider, ConversationState state, ResolvedProject project,
        PhaseDraft draft, List<FiledTicket> filed, bool mayStartRuns, CancellationToken ct) =>
        sets.FileAsync(
            provider, state, project, TicketFilingRole.Phase,
            note => renderer.RenderPhase(draft, state.JobId, note), [draft], filed, mayStartRuns, ct);

    /// <summary>
    /// 2026-09-17-0e79d: an approved cut is ONE piece of work — one work ticket from the parent
    /// draft, carrying the whole ordered set under its own spec key. What forced N tickets was
    /// never the executor: PhaseSequence splices one master-verify-record block per unexecuted
    /// phase and CommitAndPR opens one pull request per repository at the end, so the order inside
    /// one run beats a status gate — a successor cannot start before its predecessor VERIFIED.
    /// </summary>
    private Task FileEpicAsync(
        ITicketProvider provider, ConversationState state, ResolvedProject project,
        EpicOutcome epic, List<FiledTicket> filed, bool mayStartRuns, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(epic);
        // The order is the SET's, and the proposal pane ran this same orderer to show it.
        var order = orderer.Order(epic.Children);
        if (order.Error is not null)
            throw new InvalidOperationException($"The epic cannot be filed: {order.Error}.");

        return sets.FileAsync(
            provider, state, project, TicketFilingRole.Work,
            // 2026-09-13-ed5a: the work ticket records what the analysis read while it cut.
            note => renderer.RenderEpicParent(
                epic.Parent, order.Children, epic.Templates, state.JobId, note),
            order.Children, filed, mayStartRuns, ct);
    }
}
