using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: files ONE planned ticket — created with its own labels, its requires edges stored, and with a
/// series its approval record stored and its set written to its branch (<see cref="ApprovedSetTicketFiler"/>). It is
/// marked complete last: a ticket whose filing is not complete is never started. Nothing here starts a ticket.
/// Only the create runs on the caller's token; once the ticket exists, completing its filing outweighs it.
/// </summary>
public sealed class PlannedTicketFiler(
    TicketKindResolver kinds, ITicketRequirementStore requirements, ApprovedSetTicketFiler sets, TimeProvider time)
{
    public async Task FileAsync(
        ITicketProvider provider, FilingRequest request, ResolvedProject project, PlannedTicket planned,
        FilingProgress progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(planned);
        ArgumentNullException.ThrowIfNull(progress);
        var required = planned.Requires.Distinct(StringComparer.Ordinal)
            .Select(r => GraphTicketRequire.ExistingId(r) is { } existing ? (Id: existing, Title: (string?)null) : progress.Ids[r]).ToList();
        var content = planned.Render([.. required.Select(r => (FiledTicketKey.Of(project.Tracker.Type, r.Id), r.Title))]);
        var made = await provider.CreateAsync(content.Title, content.Body, planned.Labels, kinds.For(project, planned.Role), ct);
        progress.Created(planned.Key, FiledTicket.Of(made, content.Title, project), new CreatedGraphTicket(made, [], false, planned.Labels));
        var edges = Edges(request, project, made.Id.Value, [.. required.Select(r => r.Id)]);
        if (edges.Count > 0) await requirements.AddAsync(edges, CancellationToken.None);
        progress.Update(c => c with { Edges = edges });
        if (planned.Series is { } series)
            await sets.RecordAsync(provider, Source(request, planned), project, made, series, planned.Goal,
                progress.Notes, CancellationToken.None, request.Basis);
        progress.Update(c => c with { Complete = true });
    }

    private List<TicketRequirementEdge> Edges(FilingRequest request, ResolvedProject project, string id, IReadOnlyList<string> required)
    {
        var key = ApprovedSpecSetRecorder.KeyFor(project, id).Value;
        return [.. required.Select(r => new TicketRequirementEdge(project.Tracker.Name, key, id, project.Name,
            ApprovedSpecSetRecorder.KeyFor(project, r).Value, r, request.MayStartRuns, time.GetUtcNow(), request.Conversation))];
    }

    // 2026-10-14-dbcel: a list ticket's own repositories, else the approval's scope (the recorder's rule).
    private static ApprovalSource Source(FilingRequest request, PlannedTicket planned) =>
        planned.Repos.Count > 0 ? request.Approval with { Repositories = planned.Repos } : request.Approval;
}
