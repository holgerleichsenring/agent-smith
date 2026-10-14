using AgentSmith.Application.Services.Tickets;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Tickets.Runway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-8762v: the worker's tail with no act — agent-smith starts the series' next attempt itself when the
/// runway says start. A ticket a person closed is marked and left alone; a tracker it cannot read waits; a refused
/// launch and a runway hold the runway causes are said once. Every mark refreshes the ticket page, which read the
/// series before the worker decided.
/// </summary>
public sealed class SeriesAutoStart(
    IServiceScopeFactory scopes,
    ITicketProviderFactory tickets,
    TicketClosure closure,
    IReworkLaunch launch,
    ReworkSpeech speech,
    ITicketActivityNudge activity,
    ILogger<SeriesAutoStart> logger) : ISeriesAutoStart
{
    public async Task<ReworkNudgeDisposition> ContinueAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        SeriesRunwayRead? read;
        using (var scope = scopes.CreateScope())
            read = await scope.ServiceProvider.GetRequiredService<SeriesRunwayReader>().ReadAsync(project.Name, ticketId, ct);
        if (read?.NewestRunId is not { } newest || read.View.Next == SeriesHeldReasons.Complete)
            return ReworkNudgeDisposition.Finish;
        if (read.View.HeldBecause is { } code)
        {
            if (SeriesHeldReasons.Spoken.Contains(code)
                && await speech.SayOnTicketOnceAsync(project, ticketId, SeriesRunwayReader.HeldKey(newest, code), SeriesTexts.Held(code, read), ct))
                await NudgeAsync(project, ticketId, ct);
            return ReworkNudgeDisposition.Finish;
        }
        return await StartAsync(project, ticketId, newest, read, ct);
    }

    private async Task<ReworkNudgeDisposition> StartAsync(
        ResolvedProject project, string ticketId, string newest, SeriesRunwayRead read, CancellationToken ct)
    {
        try
        {
            var provider = tickets.Create(project.Tracker);
            var ticket = await provider.GetTicketAsync(new TicketId(ticketId), ct);
            if (await closure.ClosedForWorkAsync(project, provider, ticket, ct))
            {
                if (await speech.MarkOnceAsync(project.Name, ticketId, SeriesRunwayReader.ClosedKey(newest), ct))
                    await NudgeAsync(project, ticketId, ct);
                return ReworkNudgeDisposition.Finish;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Project}/#{Ticket} could not be read; its series continues on the next try", project.Name, ticketId);
            return ReworkNudgeDisposition.Reschedule;
        }
        var outcome = await launch.StartSeriesAttemptAsync(project.Name, ticketId, ct);
        if (outcome.Kind == ReworkOutcomeKind.Started)
            return outcome.RunId is null ? ReworkNudgeDisposition.Reschedule : ReworkNudgeDisposition.Finish;
        if (await speech.SayOnTicketOnceAsync(project, ticketId, SeriesRunwayReader.RefusedKey(newest),
                SeriesTexts.Refused(outcome.Reason ?? "the run could not be started", read), ct))
            await NudgeAsync(project, ticketId, ct);
        return ReworkNudgeDisposition.Finish;
    }

    // The spelling the run row's platform gives: the project's tracker type, lowercased.
    private Task NudgeAsync(ResolvedProject project, string ticketId, CancellationToken ct) =>
        activity.ChangedAsync(project.Name, TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value, ct);
}
