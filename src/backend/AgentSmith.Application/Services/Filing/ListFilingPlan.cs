using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: the planned tickets of an approved LIST, in merge order. Each ticket carries its own approved
/// specs (5701a) and is filed like an epic: one ticket per part, a later part requiring the one before, and a ticket
/// that requires a split ticket requiring its last part. Its record names the ticket's repositories when it names
/// any. A ticket without a spec, a requires cycle or a <c>ticket:&lt;id&gt;</c> the tracker lacks refuses the whole
/// list before anything exists.
/// </summary>
public sealed class ListFilingPlan(
    ITicketExistence existence, TicketGraphOrderer orderer, SeriesFilingPlan series, SeriesTicketRegion regions)
{
    public async Task<IReadOnlyList<PlannedTicket>> PlanAsync(
        FilingRequest request, ResolvedProject project, TicketGraphOutcome list, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(list);
        await RefuseMissingAsync(project, list, ct);
        var order = orderer.Order(list.Tickets);
        if (order.Error is not null) throw new InvalidOperationException($"The list cannot be filed: {order.Error}.");
        if (order.Tickets.Where(t => t.Specs.Count == 0).ToList() is { Count: > 0 } bare)
            throw new InvalidOperationException("Nothing was filed: " + string.Join("; ", bare.Select(t =>
                $"ticket '{t.Key}' ({t.Title}) has no spec — write draft:tickets/{t.Key}/<label>.yaml")) + ".");
        var last = new Dictionary<string, string>(StringComparer.Ordinal);
        var planned = new List<PlannedTicket>();
        foreach (var ticket in order.Tickets) planned.AddRange(Ticket(request, project, list, ticket, last));
        return planned;
    }

    private IReadOnlyList<PlannedTicket> Ticket(
        FilingRequest request, ResolvedProject project, TicketGraphOutcome list, GraphTicket ticket, Dictionary<string, string> last)
    {
        var parts = series.Parts(ticket.Specs, list, $"Ticket '{ticket.Key}'");
        var (labels, note) = SeriesFilingPlan.Stamp(project);
        var planned = new List<PlannedTicket>();
        for (var i = 0; i < parts.Count; i++)
        {
            var shown = ticket with { Title = SeriesFilingPlan.PartGoal(ticket.Title, i, parts.Count) };
            IReadOnlyList<string> requires = i == 0
                ? [.. ticket.Requires.Distinct(StringComparer.Ordinal).Select(r => GraphTicketRequire.ExistingId(r) is null ? last[r] : r)]
                : [planned[^1].Key];
            var region = regions.Render(shown.Title, parts[i].Id, parts[i].Drafts, request.Conversation, note);
            planned.Add(new PlannedTicket(SeriesFilingPlan.PartKey(ticket.Key, i, parts.Count), TicketFilingRole.Work,
                required => DecidedQuestionLines.Appended(GraphTicketBody.Render(shown, region, required), list.Questions),
                parts[i], shown.Title, requires, labels) { Repos = ticket.Repos });
        }
        last[ticket.Key] = planned[^1].Key;
        return planned;
    }

    private async Task RefuseMissingAsync(ResolvedProject project, TicketGraphOutcome list, CancellationToken ct)
    {
        var found = await existence.CheckAsync(
            project, [.. list.Tickets.SelectMany(t => t.Requires).Select(GraphTicketRequire.ExistingId).OfType<string>()], ct);
        if (found.Missing.Count + found.Unreadable.Count == 0) return;
        throw new InvalidOperationException("Nothing was filed: " + string.Join("; ",
            found.Missing.Select(id => $"ticket {id} does not exist on {project.Tracker.Name}")
                .Concat(found.Unreadable.Select(u => $"ticket {u.Id} could not be read ({u.Reason})"))) + ".");
    }
}
