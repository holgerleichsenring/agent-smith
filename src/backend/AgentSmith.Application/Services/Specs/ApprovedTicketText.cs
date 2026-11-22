using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-22-a720b: with an approved set the ticket's text is a transcript of its specs, so a revision an approval given
/// in a conversation caused — newer than the branch set's — rewrites the region before the branch is written: the order
/// the amend run keeps, inside the run that holds the branch, so no second writer touches it. The set then carries the
/// fingerprint of the ticket as the tracker stored it, label note stripped as the next run's fetch reads it. A text a
/// person edited since agent-smith last wrote it is replaced only by that newer approval, and never silently: the ticket
/// is told what it said. A refused rewrite costs the tracker text, never the run.
/// </summary>
public sealed class ApprovedTicketText(
    ApprovedSetRegion regions, TicketDescriptionRewrite descriptions, ITicketProviderFactory trackers,
    ILogger<ApprovedTicketText> logger)
{
    /// <summary>The head line of the comment that keeps a replaced edit.</summary>
    public const string ReplacedHeading = "Agent Smith — the ticket text was rewritten from the approved specs";

    /// <param name="previous">The set the branch held before this revision; null when the branch held none.</param>
    public async Task<SpecSet> FollowAsync(
        PipelineContext pipeline, TrackerConnection? tracker, SpecSet set, SpecSet? previous, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(set);
        if (tracker is null || previous is null || !Follows(pipeline, set, previous)
            || !pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null) return set;
        var rewrite = await descriptions.RewriteAsync(tracker, ticket.Id, regions.Render(tracker, set), null, ct);
        if (!rewrite.Rewritten)
        {
            logger.LogWarning("The text of ticket {Ticket} was not rewritten from spec set {Key}: {Reason}",
                ticket.Id.Value, set.Key, rewrite.Reason);
            return set;
        }
        if (previous.TicketFingerprint is { } written && TicketTextFingerprint.Of(ticket) != written)
            await KeepReplacedAsync(tracker, ticket, ct);
        if (await ReadBackAsync(tracker, ticket.Id, ct) is not { } stored) return set;
        pipeline.Set(ContextKeys.Ticket, stored);
        return set with { TicketFingerprint = TicketTextFingerprint.Of(stored) };
    }

    // A demand re-cut, a kept edit, a rework or a status-back revision carries no new approval from a conversation; a
    // first hand-off was filed or amended from this very set, which wrote the text already.
    private static bool Follows(PipelineContext pipeline, SpecSet set, SpecSet previous) =>
        !pipeline.Flag(ContextKeys.SpecDerivationPreview) && !pipeline.Has(ContextKeys.InlineTicket)
        && set is { IsHandedBack: false, Specs.Count: > 0, Approval: { } approval } && approval.IsNewerThan(previous.Approval)
        && set.Revisions.Count > 0 && set.Current.Cause.StartsWith(SpecRevisionCause.Approval, StringComparison.Ordinal);

    private async Task KeepReplacedAsync(TrackerConnection tracker, Ticket ticket, CancellationToken ct)
    {
        var body = $"{ReplacedHeading}<br/><br/>The text it replaced had been edited since agent-smith last wrote it:<br/><br/>"
            + System.Net.WebUtility.HtmlEncode(ticket.Description).Replace("\n", "<br/>");
        try { await trackers.Create(tracker).UpdateStatusAsync(ticket.Id, body, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The edited text ticket {Ticket} had before its rewrite could not be kept as a comment", ticket.Id.Value);
        }
    }

    private async Task<Ticket?> ReadBackAsync(TrackerConnection tracker, TicketId id, CancellationToken ct)
    {
        try { return TicketLabelNoteStripper.Strip(await trackers.Create(tracker).GetTicketAsync(id, ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Ticket} could not be read back after its text was rewritten; the next run reads it as edited", id.Value);
            return null;
        }
    }
}
