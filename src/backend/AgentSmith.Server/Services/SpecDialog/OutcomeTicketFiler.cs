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
/// factory. Bug → the fix-bug ticket shape (title + body, no label — the same shape the create-ticket
/// chat intent files). Phase → one `phase`-labelled ticket. Epic → EpicTicketFiler, which owns the
/// whole work-ticket-and-records shape. Sequential on purpose: a failure reports exactly what was created.
/// <para>
/// 2026-09-17-0e79a: filing a PHASE also stores the approved set under the created ticket's spec
/// key. The ticket body carries no spec any more, so the record is what the run works from —
/// storing it is part of filing, not a step after it. 2026-09-17-0e79d: an epic stores its whole
/// set the same way, under the WORK ticket it files, which is why the epic filer is handed the
/// session and the resolved project too.
/// </para>
/// <para>
/// 2026-09-17-042eg: every ticket it files is then reported as started, not started or a record.
/// A phase is started only AFTER its set is stored — a work ticket moved into a trigger status
/// before the record exists would be claimed by the poller and derive its own spec.
/// </para>
/// </summary>
public sealed class OutcomeTicketFiler(
    AgentSmithConfig config,
    ITicketProviderFactory ticketFactory,
    PhaseTicketRenderer renderer,
    BugTicketRenderer bugRenderer,
    EpicTicketFiler epicFiler,
    ApprovedPhaseSetRecorder approvals,
    FiledWorkStarter starter, TicketKindResolver kinds,
    ILogger<OutcomeTicketFiler> logger)
{
    public async Task<FilingReport> FileAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
        CancellationToken cancellationToken)
    {
        var filed = new List<FiledTicket>();
        var notes = new List<string>();
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
                EpicOutcome epic => epicFiler.FileAsync(
                    provider, state, project, epic, filed, notes, mayStartRuns, cancellationToken),
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

    private ResolvedProject ResolveProject(ConversationState state)
    {
        var project = state.Scope?.Project ?? state.Project;
        if (string.IsNullOrWhiteSpace(project))
            throw new InvalidOperationException(
                "The spec-dialog session has no active-scope project to file tickets into.");
        if (!config.Projects.TryGetValue(project, out var resolved))
            throw new InvalidOperationException(
                $"Active-scope project '{project}' is not in the configuration catalog.");
        return resolved;
    }

    // A bug carries NO framework label, so the project's own rules decide which pipeline claims
    // it — "started" here is any match naming this project, not the phase-execution preset.
    private async Task FileBugAsync(
        ITicketProvider provider, ResolvedProject project, BugTicketDraft ticket,
        List<FiledTicket> filed, bool mayStartRuns, CancellationToken ct)
    {
        // 2026-09-15-6d9c: rendered, not composed here — the proposal pane shows the same
        // body before this runs, and two copies of it would drift apart.
        var body = bugRenderer.RenderBody(ticket);
        var title = TicketTitle.Fit(ticket.Title);
        var created = await provider.CreateAsync(
            title, body, labels: [], kinds.For(project, TicketFilingRole.Bug), ct);
        filed.Add(Entry(created, title, project));
        await starter.StampAsync(provider, project, created, [], mayStartRuns, filed, ct);
    }

    private async Task FilePhaseAsync(
        ITicketProvider provider, ConversationState state, ResolvedProject project,
        PhaseDraft draft, List<FiledTicket> filed, bool mayStartRuns, CancellationToken ct)
    {
        var content = renderer.RenderPhase(draft, state.JobId);
        string[] labels = [PhaseTicketRenderer.PhaseLabel, FiledTicketLabels.ApprovedSetStamp];
        var created = await provider.CreateAsync(
            content.Title, content.Body, labels, kinds.For(project, TicketFilingRole.Phase), ct);
        filed.Add(Entry(created, content.Title, project));
        await approvals.RecordAsync(state, project, created.Id.Value, [draft], ct);
        await starter.StampAsync(provider, project, created, labels, mayStartRuns, filed, ct);
    }

    /// <summary>The id, the project and the display key travel on the report: a Reference is a web
    /// url wherever the tracker gives one, and the ticket's runs are found by project and id.</summary>
    internal static FiledTicket Entry(CreatedTicket created, string title, ResolvedProject project) =>
        new(created.Reference, title)
        { TicketId = created.Id.Value, Project = project.Name, Key = FiledTicketKey.Of(project, created) };
}
