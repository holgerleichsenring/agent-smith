using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-28-7a69b: the description half of an amendment — a courtesy for whoever reads the ticket,
/// never blocking, because a run reads the record and the branch. Moved out of
/// <see cref="TicketAmendment"/> when the placement joined it.
/// <para>
/// An unmarked body the FRAMEWORK rendered whole — stamped as an approved set, filed as an epic
/// record, or holding a recorded spec set — stays refused: nothing says where its text ends. Every
/// other unmarked body is a person's, and the region goes below it. The labels decide rather than
/// "was there a record before", because a first append that failed still leaves a record behind.
/// </para>
/// </summary>
public sealed class TicketDescriptionRewrite(ITicketProviderFactory trackers, ILogger<TicketDescriptionRewrite> logger)
{
    /// <summary>The sentence the thread is told about the description.</summary>
    public async Task<string> WriteAsync(
        ResolvedProject project, TicketId id, string region, SpecApprovalRecord? previous, CancellationToken ct)
    {
        try
        {
            var placement = await PlacementAsync(project, id, previous, ct);
            var rewrite = await trackers.CreateRewriter(project.Tracker).RewriteRegionAsync(id, region, placement, ct);
            return rewrite.Rewritten ? "The ticket's description now says what it says."
                : $"The ticket's description was not rewritten: {rewrite.Reason}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Rewriting the description of ticket {Ticket} failed", id.Value);
            return $"The ticket's description was not rewritten: {ex.Message}.";
        }
    }

    private async Task<RegionPlacement> PlacementAsync(
        ResolvedProject project, TicketId id, SpecApprovalRecord? previous, CancellationToken ct)
    {
        if (previous is { TicketText: false }) return RegionPlacement.ReplaceOnly;
        var labels = (await trackers.Create(project.Tracker).GetTicketAsync(id, ct)).Labels ?? [];
        return FiledTicketLabels.CarriesApprovedSet(labels, TicketLabelVocabulary.For(project.Tracker))
            || FiledTicketLabels.IsEpicRecord(new IncomingTicketEnvelope { Labels = labels })
            ? RegionPlacement.ReplaceOnly : RegionPlacement.ReplaceOrAppend;
    }
}
