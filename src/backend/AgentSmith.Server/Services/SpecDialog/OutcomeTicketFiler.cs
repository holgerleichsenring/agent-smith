using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315c: files a confirmed outcome into the ACTIVE SCOPE's tracker via the
/// existing provider factory. Bug → the fix-bug ticket shape (title + body,
/// no label — the same shape the create-ticket chat intent files). Phase →
/// one `phase`-labelled ticket. Epic → EpicTicketFiler, which owns the whole
/// parent-and-children shape. Sequential on purpose: a failure reports exactly
/// what was created.
/// </summary>
public sealed class OutcomeTicketFiler(
    AgentSmithConfig config,
    ITicketProviderFactory ticketFactory,
    PhaseTicketRenderer renderer,
    BugTicketRenderer bugRenderer,
    EpicTicketFiler epicFiler,
    ILogger<OutcomeTicketFiler> logger)
{
    public async Task<FilingReport> FileAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        var filed = new List<FiledTicket>();
        var notes = new List<string>();
        try
        {
            var provider = ResolveProvider(state);
            await (proposal switch
            {
                BugOutcome bug => FileBugAsync(provider, bug.Ticket, filed, cancellationToken),
                PhaseOutcome phase => FilePhaseAsync(provider, phase.Draft, filed, cancellationToken),
                EpicOutcome epic => epicFiler.FileAsync(provider, epic, filed, notes, cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Outcome kind '{proposal.GetType().Name}' cannot be filed."),
            });
            return new FilingReport(filed, Error: null) { Notes = notes };
        }
        // A tracker timeout is a filing failure the report names; only the caller's cancellation escapes.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "Ticket filing failed for spec-dialog session {SessionId} after {Count} ticket(s)",
                state.JobId, filed.Count);
            return new FilingReport(filed, ex.Message) { Notes = notes };
        }
    }

    private ITicketProvider ResolveProvider(ConversationState state)
    {
        var project = state.Scope?.Project ?? state.Project;
        if (string.IsNullOrWhiteSpace(project))
            throw new InvalidOperationException(
                "The spec-dialog session has no active-scope project to file tickets into.");
        if (!config.Projects.TryGetValue(project, out var resolved))
            throw new InvalidOperationException(
                $"Active-scope project '{project}' is not in the configuration catalog.");
        return ticketFactory.Create(resolved.Tracker);
    }

    private async Task FileBugAsync(
        ITicketProvider provider, BugTicketDraft ticket,
        List<FiledTicket> filed, CancellationToken ct)
    {
        // 2026-09-15-6d9c: rendered, not composed here — the proposal pane shows the same
        // body before this runs, and two copies of it would drift apart.
        var body = bugRenderer.RenderBody(ticket);
        var created = await provider.CreateAsync(ticket.Title, body, labels: [], ct);
        filed.Add(new FiledTicket(created.Reference, ticket.Title));
    }

    private async Task FilePhaseAsync(
        ITicketProvider provider, PhaseDraft draft,
        List<FiledTicket> filed, CancellationToken ct)
    {
        var content = renderer.RenderPhase(draft);
        var created = await provider.CreateAsync(
            content.Title, content.Body, [PhaseTicketRenderer.PhaseLabel], ct);
        filed.Add(new FiledTicket(created.Reference, content.Title));
    }
}
