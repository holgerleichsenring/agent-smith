using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-0e79c: delivers the hand-back — the false premises, on the ticket and on the run.
/// <para>
/// The step FAILS, and a failing step runs the finalizer tail: when the run delivers what
/// verified as a shortfall, the error handler's own failure comment never runs. So the finding
/// reaches the ticket only if this posts it. A tracker that refuses the comment must not change
/// the verdict — the phase row and the run decision carry it either way.
/// </para>
/// <para>
/// It does NOT say the same thing twice. A phase is marked executed only at WritePhaseRecord, so
/// every re-trigger re-enters the phase and re-asks; without this guard five re-triggers leave
/// five identical comments and teach the operator to ignore the signal. The rule is 0e79b's and
/// bd7a's: our own marker on the thread means it was said, unless somebody replied after it —
/// a reply is a person engaging, and then saying it again is answering them.
/// </para>
/// </summary>
public sealed class PremiseHandbackNotice(
    ITicketProviderFactory ticketFactory,
    IDecisionLogger decisions,
    ILogger<PremiseHandbackNotice> logger)
{
    /// <summary>True when the ticket was actually told by THIS call.</summary>
    public async Task<bool> PostAsync(
        PipelineContext pipeline, TrackerConnection? tracker, PhaseDraft draft,
        IReadOnlyList<PremiseFinding> findings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(findings);
        if (findings.Count == 0) return false;
        var body = PremiseHandback.Notice(draft, findings, HasPullRequest(pipeline));
        // The run's own record of it, whether or not a tracker is reachable. A run is a new
        // reader every time, so this is written on every run that hands back.
        await decisions.LogAsync(null, DecisionCategory.Implementation, body, ct, "spec-set");
        if (tracker is null) return false;
        if (!pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null) return false;
        // An inline ticket exists only on this run — there is nothing to comment on.
        if (pipeline.Has(ContextKeys.InlineTicket)) return false;
        if (AlreadyTold(pipeline, draft.PhaseId))
        {
            logger.LogInformation(
                "Ticket {Ticket} was already told that phase {Phase} rests on a premise that no "
                + "longer holds, and nobody has replied — not saying it again",
                ticket.Id.Value, draft.PhaseId);
            return false;
        }

        try
        {
            await ticketFactory.Create(tracker).UpdateStatusAsync(ticket.Id, body, ct);
            logger.LogInformation(
                "Told ticket {Ticket} that phase {Phase} rests on a premise that no longer holds",
                ticket.Id.Value, draft.PhaseId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Could not post the premise hand-back to ticket {Ticket}", ticket.Id.Value);
            return false;
        }
    }

    // Said before on this thread, and nobody has answered it since.
    private static bool AlreadyTold(PipelineContext pipeline, string phaseId)
    {
        var comments = pipeline.TryGet<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments, out var c) ? c : null;
        var marker = PremiseHandback.Marker(phaseId);
        return comments is not null
            && comments.Any(comment => comment.Body?.Contains(marker, StringComparison.Ordinal) == true)
            && !OwnTicketComment.IsAnswered(comments, marker);
    }

    // The spec pull request exists only once the derivation published; a hand-back on the first
    // phase of a set happens before any delivery, and must not send a reader to one.
    private static bool HasPullRequest(PipelineContext pipeline) =>
        (pipeline.TryGet<string>(ContextKeys.SpecPullRequestUrl, out var spec)
         && !string.IsNullOrWhiteSpace(spec))
        || (pipeline.TryGet<string>(ContextKeys.PullRequestUrl, out var pr)
            && !string.IsNullOrWhiteSpace(pr));
}
