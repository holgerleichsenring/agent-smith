using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-13-9a59m: what makes a ticket claimable — resolved the way the POLLER would (labels, ticket id
/// and platform), moved into a trigger status only when that resolution names the project, the move
/// needing runs.control. Extracted from <see cref="FiledWorkStarter"/>, which starts a ticket it just
/// filed; an approval in a never-worked ticket's own conversation starts that EXISTING ticket here too.
/// </summary>
public sealed class FiledWorkMove(
    AgentSmithConfig config,
    IEnvelopeProjectResolver resolver,
    FiledWorkTagger tagger,
    ILogger<FiledWorkMove> logger,
    IStartupFindings? findings = null)
{
    /// <summary>
    /// Starts a ticket that already exists: the project's routing tag only, resolved against the labels
    /// the ticket CARRIES — never a spec label or the approved-set stamp, which would send a discovered
    /// ticket through the filed-ticket gate instead of deriving. Never throws.
    /// </summary>
    public async Task<FiledWorkStart> StartExistingAsync(
        ITicketProvider provider, ResolvedProject project, string ticketId, bool mayStartRuns, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var id = new TicketId(ticketId);
        IReadOnlyList<string> labels;
        try { labels = (await provider.GetTicketAsync(id, ct)).Labels; }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Ticket} could not be read to start it", ticketId);
            return NotStarted(FiledWorkReasons.StatusUnreadable);
        }
        var tagged = await tagger.ApplyAsync(provider, project, new CreatedTicket(id, null), labels, ct);
        return tagged.Explaining(await StartAsync(provider, project, id, tagged.Labels, mayStartRuns, ct));
    }

    /// <summary>Resolves and moves a ticket whose labels are <paramref name="labels"/>. Never throws.</summary>
    public async Task<FiledWorkStart> StartAsync(
        ITicketProvider provider, ResolvedProject project, TicketId ticket, IReadOnlyList<string> labels, bool mayStartRuns, CancellationToken ct)
    {
        var platform = project.Tracker.Type.ToString().ToLowerInvariant();
        var envelope = new IncomingTicketEnvelope
            { Labels = labels, TicketId = ticket.Value, Platform = platform };
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
        ITicketProvider provider, ResolvedProject project, TicketId ticket, WebhookTriggerConfig trigger, bool mayStartRuns, CancellationToken ct)
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
        try { await provider.TransitionToAsync(ticket, target, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Moving filed ticket {Ticket} to '{Status}' threw", ticket.Value, target);
            return NotStarted(FiledWorkReasons.MoveFailed(target, ex.Message));
        }
        var after = await StatusAsync(provider, ticket, ct);
        return after is not null && FiledWorkReasons.Triggers(trigger, after)
            ? new FiledWorkStart(FiledStartState.Started, FiledWorkReasons.Moved(after))
            : NotStarted(FiledWorkReasons.MoveHeld(target, after ?? status));
    }

    // A status nobody could read is a ticket nobody may claim started; it is never an exception,
    // because the tickets it is about exist and the report has to name every one of them.
    private async Task<string?> StatusAsync(ITicketProvider provider, TicketId ticket, CancellationToken ct)
    {
        try { return (await provider.GetTicketAsync(ticket, ct)).Status; }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The status of filed ticket {Ticket} could not be read", ticket.Value);
            return null;
        }
    }

    private static FiledWorkStart NotStarted(string reason) => new(FiledStartState.NotStarted, reason);
}
