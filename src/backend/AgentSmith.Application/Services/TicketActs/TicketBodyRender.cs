using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-11-17-dd05b: a ticket's body re-rendered from the specs on its branch — the branch is the only spec truth, so
/// the body says what it holds. The region is the filing's (<see cref="SeriesTicketRegion"/>), written by the
/// amendment's description rewrite; then the branch manifest takes the fingerprint of the ticket as the tracker stores
/// it, so the next run does not read this system's rewrite as a person's edit.
/// </summary>
public sealed class TicketBodyRender(
    TicketBranchSeries branch, SeriesTicketRegion regions, TicketDescriptionRewrite descriptions,
    ITicketProviderFactory trackers, SeriesFingerprintStamp stamp)
{
    public async Task<TicketBodyOutcome> RenderAsync(ResolvedProject project, TicketBodyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(request);
        var id = new TicketId(request.TicketId);
        var read = await branch.ReadAsync(project, ApprovedSpecSetRecorder.KeyFor(project, request.TicketId), id, ct);
        if (read.Set is not { Specs.Count: > 0 } set)
            return new(false, $"Ticket {request.TicketId} has no specs on its branch{Why(read)}, so there is nothing to render its body from.");
        // A person asked for it: a body whose region a person removed takes it again below their text, never over it.
        var rewrite = await descriptions.RewriteAsync(project, id, Region(project, set), null, ct, RegionPlacement.ReplaceOrAppend);
        if (!rewrite.Rewritten)
            return new(false, $"The body of ticket {request.TicketId} was not rewritten: {rewrite.Reason}");
        var failed = await stamp.StampAsync(project, set, await trackers.Create(project.Tracker).GetTicketAsync(id, ct), ct);
        return failed is null
            ? new(true, $"The body of ticket {request.TicketId} now says what its {set.Specs.Count} spec(s) on the branch say.")
            : new(true, $"The body of ticket {request.TicketId} was rewritten, but its branch keeps the old ticket fingerprint "
                + $"({failed}): the next run reports the ticket as edited once.");
    }

    private string Region(ResolvedProject project, SpecSet set)
    {
        var stamp = TicketLabelVocabulary.For(project.Tracker).ApprovedSetStamp;
        return regions.Render(set.Goal ?? string.Empty, set.Series ?? set.Key, [.. set.Specs.Select(s => s.Draft)],
            set.Approval?.Conversation, TicketLabelNote.For([stamp], stamp)).Body;
    }

    private static string Why(SpecSetOnBranch read) => read.Why is { Length: > 0 } why ? $" ({why})" : string.Empty;
}
