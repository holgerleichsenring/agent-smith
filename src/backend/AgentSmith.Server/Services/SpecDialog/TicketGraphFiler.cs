using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcel: files an approved LIST of tickets. Every <c>ticket:&lt;id&gt;</c> it requires is read first, and
/// one the tracker does not have refuses the whole list before anything exists. Then, in merge order, each ticket is
/// created as a work ticket whose body is its text, its requires edges are stored, and its ticket-text approval record
/// is stored LAST — the mark that its filing is complete. A failure stops the loop and names what was not created; a
/// cancellation once a ticket exists stops it too, and everything after runs without the caller's token, because a
/// root nobody starts never wakes its dependents. <see cref="TicketGraphStarts"/> then decides each start.
/// </summary>
public sealed class TicketGraphFiler(
    AgentSmithConfig config, ITicketProviderFactory ticketFactory, ITicketExistence existence,
    TicketGraphOrderer orderer, ITicketRequirementStore requirements, GraphTicketApproval approvals,
    TicketGraphStarts starts, TicketKindResolver kinds, TimeProvider time, ILogger<TicketGraphFiler> logger)
{
    public async Task<FilingReport> FileAsync(
        ConversationState state, TicketGraphOutcome graph, bool mayStartRuns, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(graph);
        var (filed, notes, created) = (new List<FiledTicket>(), new List<string>(), new List<CreatedGraphTicket>());
        var ids = new Dictionary<string, (string Id, string Title)>(StringComparer.Ordinal);
        (ResolvedProject? project, ITicketProvider? provider, IReadOnlyList<string> labels) = (null, null, []);
        string? error = null;
        try
        {
            project = OutcomeTicketFiler.ResolveProject(config, state);
            provider = ticketFactory.Create(project.Tracker);
            await RefuseMissingAsync(project, graph, ct);
            var order = orderer.Order(graph.Tickets);
            if (order.Error is not null) throw new InvalidOperationException($"The list cannot be filed: {order.Error}.");
            labels = ApprovedSetTicketFiler.Labels(project);
            foreach (var ticket in order.Tickets)
            {
                if (created.Count > 0) ct.ThrowIfCancellationRequested();
                await FileOneAsync(provider, state, project, ticket, labels, ids, mayStartRuns, filed, created, ct);
            }
        }
        // Before the first ticket the caller's cancellation escapes as it always did; after it, the filing reports.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested || filed.Count > 0)
        {
            error = ex is OperationCanceledException && ct.IsCancellationRequested
                ? $"The filing was interrupted after {filed.Count} of {graph.Tickets.Count} tickets." : ex.Message;
            logger.LogError(ex, "Filing a list for spec-dialog session {SessionId} stopped after {Count} ticket(s)", state.JobId, filed.Count);
            if (graph.Tickets.Where(t => !ids.ContainsKey(t.Key)).ToList() is { Count: > 0 } rest && filed.Count > 0)
                notes.Add($"Not created: {string.Join(", ", rest.Select(t => $"{t.Key} — {t.Title}"))}.");
        }
        if (project is not null && provider is not null && created.Count > 0)
            notes.AddRange(await starts.SettleAsync(provider, project, created, labels, mayStartRuns, filed));
        return new FilingReport(filed, error) { Notes = notes };
    }

    private async Task FileOneAsync(
        ITicketProvider provider, ConversationState state, ResolvedProject project, GraphTicket ticket,
        IReadOnlyList<string> labels, Dictionary<string, (string Id, string Title)> ids, bool mayStartRuns,
        List<FiledTicket> filed, List<CreatedGraphTicket> created, CancellationToken ct)
    {
        var required = ticket.Requires.Distinct(StringComparer.Ordinal)
            .Select(r => GraphTicketRequire.ExistingId(r) is { } existing ? (Id: existing, Title: (string?)null) : ids[r]).ToList();
        var content = GraphTicketBody.Render(ticket, [.. required.Select(r => (FiledTicketKey.Of(project.Tracker.Type, r.Id), r.Title))],
            state.JobId, TicketLabelNote.For(labels, labels[0]));
        var made = await provider.CreateAsync(content.Title, content.Body, labels, kinds.For(project, TicketFilingRole.Work), ct);
        filed.Add(FiledTicket.Of(made, content.Title, project));
        ids[ticket.Key] = (made.Id.Value, content.Title);
        created.Add(new CreatedGraphTicket(made, [], Complete: false));
        // The ticket exists: completing its filing outweighs the caller's token, and nothing can start it before its record.
        var key = ApprovedSpecSetRecorder.KeyFor(project, made.Id.Value).Value;
        List<TicketRequirementEdge> edges = [.. required.Select(r => new TicketRequirementEdge(project.Tracker.Name, key, made.Id.Value,
            project.Name, ApprovedSpecSetRecorder.KeyFor(project, r.Id).Value, r.Id, mayStartRuns, time.GetUtcNow(), state.JobId))];
        if (edges.Count > 0) await requirements.AddAsync(edges, CancellationToken.None);
        created[^1] = created[^1] with { Edges = edges };
        await approvals.RecordAsync(state, project, made.Id.Value, ticket, CancellationToken.None);
        created[^1] = created[^1] with { Complete = true };
    }

    private async Task RefuseMissingAsync(ResolvedProject project, TicketGraphOutcome graph, CancellationToken ct)
    {
        var found = await existence.CheckAsync(
            project, [.. graph.Tickets.SelectMany(t => t.Requires).Select(GraphTicketRequire.ExistingId).OfType<string>()], ct);
        if (found.Missing.Count + found.Unreadable.Count == 0) return;
        throw new InvalidOperationException("Nothing was filed: " + string.Join("; ",
            found.Missing.Select(id => $"ticket {id} does not exist on {project.Tracker.Name}")
                .Concat(found.Unreadable.Select(u => $"ticket {u.Id} could not be read ({u.Reason})"))) + ".");
    }
}
