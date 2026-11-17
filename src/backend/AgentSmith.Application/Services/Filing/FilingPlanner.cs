using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: turns an approved proposal into the ordered tickets a filing creates — a spec one ticket of one
/// spec, an epic and a list one ticket per part, a bug one ticket with no series and no label. It reads the tracker
/// only to check what a list requires exists, and refuses before anything is created.
/// </summary>
public sealed class FilingPlanner(SeriesFilingPlan series, ListFilingPlan lists, BugTicketRenderer bugs)
{
    public async Task<FilingPlan> PlanAsync(FilingRequest request, ResolvedProject project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(project);
        return new FilingPlan(request.Proposal switch
        {
            BugOutcome bug => [Bug(bug.Ticket)],
            SpecOutcome spec => series.Spec(request, project, spec),
            EpicOutcome epic => series.Epic(request, project, epic),
            TicketGraphOutcome list => await lists.PlanAsync(request, project, list, ct),
            var other => throw new InvalidOperationException($"Outcome kind '{other.GetType().Name}' cannot be filed."),
        });
    }

    // A bug carries NO framework label, so the project's own rules decide which pipeline claims it. 2026-09-15-6d9c:
    // rendered, not composed here — the pane shows the same body first.
    private PlannedTicket Bug(BugTicketDraft ticket) =>
        new("bug", TicketFilingRole.Bug, _ => new SpecTicketContent(TicketTitle.Fit(ticket.Title), bugs.RenderBody(ticket)),
            Series: null, ticket.Title, [], []);
}
