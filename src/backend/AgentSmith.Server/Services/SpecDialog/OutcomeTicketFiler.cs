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
/// one `phase`-labelled ticket. Epic → parent first, then children in slice
/// order (each referencing the parent in its body), then a parent comment
/// listing the filed slices — body references, not tracker-native links,
/// because ITicketProvider has no linking vocabulary on any tracker today.
/// Sequential on purpose: a failure reports exactly what was created.
/// </summary>
public sealed class OutcomeTicketFiler(
    AgentSmithConfig config,
    ITicketProviderFactory ticketFactory,
    PhaseTicketRenderer renderer,
    ILogger<OutcomeTicketFiler> logger)
{
    public async Task<FilingReport> FileAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        var filed = new List<FiledTicket>();
        try
        {
            var provider = ResolveProvider(state);
            await (proposal switch
            {
                BugOutcome bug => FileBugAsync(provider, bug.Ticket, filed, cancellationToken),
                PhaseOutcome phase => FilePhaseAsync(provider, phase.Draft, filed, cancellationToken),
                EpicOutcome epic => FileEpicAsync(provider, epic, filed, cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Outcome kind '{proposal.GetType().Name}' cannot be filed."),
            });
            return new FilingReport(filed, Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Ticket filing failed for spec-dialog session {SessionId} after {Count} ticket(s)",
                state.JobId, filed.Count);
            return new FilingReport(filed, ex.Message);
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

    private static async Task FileBugAsync(
        ITicketProvider provider, BugTicketDraft ticket,
        List<FiledTicket> filed, CancellationToken ct)
    {
        var body = string.IsNullOrWhiteSpace(ticket.AcceptanceCriteria)
            ? ticket.Description
            : $"{ticket.Description}\n\n## Acceptance criteria\n{ticket.AcceptanceCriteria}";
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

    private async Task FileEpicAsync(
        ITicketProvider provider, EpicOutcome epic,
        List<FiledTicket> filed, CancellationToken ct)
    {
        var parentContent = renderer.RenderEpicParent(epic.Parent, epic.Children);
        var parent = await provider.CreateAsync(
            parentContent.Title, parentContent.Body, [PhaseTicketRenderer.PhaseLabel], ct);
        filed.Add(new FiledTicket(parent.Reference, parentContent.Title));

        var childRefs = new List<string>();
        foreach (var child in epic.Children)
        {
            var content = renderer.RenderPhase(child, parent.Reference);
            var created = await provider.CreateAsync(
                content.Title, content.Body, [PhaseTicketRenderer.PhaseLabel], ct);
            filed.Add(new FiledTicket(created.Reference, content.Title));
            childRefs.Add($"{created.Reference} — `{child.PhaseId}` {child.Goal}");
        }
        await provider.UpdateStatusAsync(
            parent.Id, $"Slices filed:\n{string.Join("\n", childRefs.Select(r => $"- {r}"))}", ct);
    }
}
