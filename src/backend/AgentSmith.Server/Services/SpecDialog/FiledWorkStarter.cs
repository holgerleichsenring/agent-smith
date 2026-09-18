using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042eg: filing created the ticket; this is what makes a run pick it up. It resolves
/// the ticket the way the POLLER would — the envelope the poller builds carries labels, ticket id
/// and platform and nothing else — and moves it into a trigger status only when that resolution
/// names the filing project.
/// <para>
/// RESOLVE FIRST, MOVE ONLY WHAT RESOLVES. A ticket nothing would route gains nothing from a move:
/// it would sit in a trigger status no poll ever claims, and reporting it started would be false.
/// So it reports NOT STARTED and says WHICH of the resolver's three silent drops happened — a
/// blocking startup finding, a resolution the ticket does not satisfy, or the project's own
/// pipeline rules — because those want three different things done about them. Stamping the
/// resolution tag on what it files is a successor's.
/// </para>
/// <para>
/// MOVING A TICKET STARTS A RUN, so it needs runs.control. Where the created status already
/// triggers, the ticket starts as it always did — the permission governs the MOVE and nothing
/// else. Without it the reason names a move in the TRACKER rather than a wait: the bool is
/// sampled once, on the request that opened the turn, and no later path re-reads it.
/// </para>
/// </summary>
public sealed class FiledWorkStarter(
    AgentSmithConfig config,
    IEnvelopeProjectResolver resolver,
    ILogger<FiledWorkStarter> logger,
    IStartupFindings? findings = null)
{
    /// <summary>
    /// Starts the ticket if it can be started, and stamps what happened onto its entry in
    /// <paramref name="filed"/>. Never throws: a tracker that refuses the move is a reason on the
    /// report, and the rest of the filing carries on.
    /// </summary>
    public async Task StampAsync(
        ITicketProvider provider, ResolvedProject project, CreatedTicket ticket,
        IReadOnlyList<string> labels, bool mayStartRuns, List<FiledTicket> filed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(filed);
        var start = await StartAsync(provider, project, ticket, labels, mayStartRuns, cancellationToken);
        logger.LogInformation(
            "Filed ticket {Ticket} is {State}: {Reason}", ticket.Id.Value, start.State, start.Reason);
        var at = filed.FindIndex(t => t.TicketId == ticket.Id.Value);
        if (at >= 0) filed[at] = filed[at] with { Start = start };
    }

    private async Task<FiledWorkStart> StartAsync(
        ITicketProvider provider, ResolvedProject project, CreatedTicket ticket,
        IReadOnlyList<string> labels, bool mayStartRuns, CancellationToken ct)
    {
        var platform = project.Tracker.Type.ToString().ToLowerInvariant();
        var envelope = new IncomingTicketEnvelope
            { Labels = labels, TicketId = ticket.Id.Value, Platform = platform };
        var trigger = TriggerSelectionHelper.ByKind(project, platform);
        if (trigger is null) return NotStarted(FiledWorkReasons.NoTrigger(project, platform));
        // A MATCH naming the filing project is the whole test, for a work ticket and for a bug
        // alike; the resolver decides the pipeline. When it names other projects instead, the
        // ticket IS routed — somewhere else — and the reason says so rather than "nothing".
        var matches = resolver.Resolve(config, envelope);
        if (!matches.Any(m => m.ProjectName == project.Name && m.Kind == platform))
            return NotStarted(FiledWorkReasons.NotRouted(
                project, platform, trigger, envelope, matches,
                findings?.BlockingReason(project.Name, TriggerKinds.ForMatchKind(platform))));
        // TrackerPoller allows any status when the operator named none.
        if (trigger.TriggerStatuses.Count == 0)
            return new FiledWorkStart(FiledStartState.Started, FiledWorkReasons.EveryStatusTriggers);
        return await MoveAsync(provider, project, ticket, trigger, mayStartRuns, ct);
    }

    private async Task<FiledWorkStart> MoveAsync(
        ITicketProvider provider, ResolvedProject project, CreatedTicket ticket,
        WebhookTriggerConfig trigger, bool mayStartRuns, CancellationToken ct)
    {
        var status = await StatusAsync(provider, ticket, ct);
        if (status is null) return NotStarted(FiledWorkReasons.StatusUnreadable);
        if (FiledWorkReasons.Triggers(trigger, status))
            return new FiledWorkStart(FiledStartState.Started, FiledWorkReasons.AlreadyTriggering(status));
        var target = trigger.TriggerStatuses[0];
        // The impossible move first: it is the same answer whoever is asking, and sending a
        // reader without the permission to find someone who holds it would waste their time on a
        // move nobody can make.
        if (!FiledWorkReasons.IsNativeState(project.Tracker.Type, target))
            return NotStarted(FiledWorkReasons.NotNative(project.Tracker.Type, target));
        if (!mayStartRuns) return NotStarted(FiledWorkReasons.NoPermission(status, target));
        try { await provider.TransitionToAsync(ticket.Id, target, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Moving filed ticket {Ticket} to '{Status}' threw", ticket.Id.Value, target);
            return NotStarted(FiledWorkReasons.MoveFailed(target, ex.Message));
        }
        var after = await StatusAsync(provider, ticket, ct);
        return after is not null && FiledWorkReasons.Triggers(trigger, after)
            ? new FiledWorkStart(FiledStartState.Started, FiledWorkReasons.Moved(after))
            : NotStarted(FiledWorkReasons.MoveHeld(target, after ?? status));
    }

    // A status nobody could read is a ticket nobody may claim started; it is never an exception,
    // because the tickets it is about exist and the report has to name every one of them.
    private async Task<string?> StatusAsync(
        ITicketProvider provider, CreatedTicket ticket, CancellationToken ct)
    {
        try { return (await provider.GetTicketAsync(ticket.Id, ct)).Status; }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The status of filed ticket {Ticket} could not be read", ticket.Id.Value);
            return null;
        }
    }

    private static FiledWorkStart NotStarted(string reason) => new(FiledStartState.NotStarted, reason);
}
