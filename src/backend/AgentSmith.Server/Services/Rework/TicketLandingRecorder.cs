using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-14-dbceg: writes a ticket's landing once its pull requests ended with one merged — the
/// rule is <see cref="TicketLanding.OfStates"/>, the one the ticket list reads. The unique insert is
/// the transition: only the replica whose insert won logs it and announces it. The row is the fact,
/// Redis only announces it, so a failed announcement is logged and never fails the call.
/// 2026-10-14-dbcei: a person's by-hand mark goes through the same insert, wake and announcements.
/// </summary>
public sealed class TicketLandingRecorder(
    IServiceScopeFactory scopes,
    ITicketLandingStore landings, DependentsWake wake, // 2026-10-14-dbceh: the won insert wakes, before any Redis call
    ISystemEventPublisher events,
    ITicketActivityNudge activity,
    TimeProvider clock,
    ILogger<TicketLandingRecorder> logger)
{
    public const string Source = "ticket-landing";

    /// <summary>Records the landing when the ticket's records say merged; answers whether THIS call inserted it.</summary>
    public async Task<bool> RecordMergedAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        IReadOnlyList<PrReviewRecordView> records;
        using (var scope = scopes.CreateScope())
            records = await scope.ServiceProvider.GetRequiredService<IPrReviewRecordReader>().ByTicketAsync(project.Name, ticketId, ct);
        if (TicketLanding.OfStates([.. records.Select(r => r.State)]) != TicketLanding.Merged) return false;
        var newestMerged = records.Where(r => r.State == PrReviewStates.Merged)
            .OrderBy(r => r.StateAt ?? DateTimeOffset.MinValue).Last();
        var landedAt = records.Max(r => r.StateAt) ?? clock.GetUtcNow();
        return await RecordAsync(new TicketLandingFact(project.Tracker.Name, KeyOf(project, ticketId), ticketId, project.Name,
            TicketLandingKinds.Merged, landedAt, null, null, newestMerged.Url), ct);
    }

    /// <summary>Records a person's mark that the ticket landed; the stored landing, or null when it had already landed.</summary>
    public async Task<TicketLandingFact?> RecordByHandAsync(
        ResolvedProject project, string ticketId, string by, string note, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(note);
        var landing = new TicketLandingFact(project.Tracker.Name, KeyOf(project, ticketId), ticketId, project.Name,
            TicketLandingKinds.ByHand, clock.GetUtcNow(), by, note.Trim(), null);
        return await RecordAsync(landing, ct) ? landing : null;
    }

    internal static string KeyOf(ResolvedProject project, string ticketId) =>
        TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;

    private async Task<bool> RecordAsync(TicketLandingFact landing, CancellationToken ct)
    {
        if (!await landings.TryRecordAsync(landing, ct)) return false;
        logger.LogInformation("Ticket {Project}/{Ticket} landed ({Kind}) at {LandedAt}",
            landing.Project, landing.TicketId, landing.Kind, landing.LandedAt);
        await wake.WakeAsync(landing, ct);
        try
        {
            await events.PublishAsync(new TicketLandedEvent(Source, landing.Project, landing.TicketKey, landing.TicketId,
                landing.Kind, landing.By, clock.GetUtcNow()), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The landing of {Project}/{Ticket} is stored but was not announced", landing.Project, landing.TicketId);
        }
        try
        {
            await activity.ChangedAsync(landing.Project, landing.TicketKey, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The ticket pages of {Project}/{Ticket} were not told of its landing", landing.Project, landing.TicketId);
        }
        return true;
    }
}
