using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// 2026-11-27-3532a: the spawn funnel's outside entry. A poller or webhook spawn of the code pipeline — not by name, with no plan
/// answers for a parked run — claims nothing: the ticket enters its series (<see cref="ISeriesTicketEntry"/>) and is answered
/// <see cref="ClaimRejectionReason.LedBySeries"/>. A ticket with a live lease goes through the funnel as before. Of a queued
/// ticket, only an entry a start by name did not put there (a backstop with no context, a trigger's entry from before) goes
/// through the funnel, so a queue head is still claimed; a start by name keeps its entry and its context, and the pump claims
/// it. A process with no series entry claims as before.
/// </summary>
internal sealed class SeriesLedSpawn(ISeriesTicketEntry? entry, IActiveRunLease leases, ICapacityQueue queue, ILogger logger)
{
    /// <summary>The answer when the ticket's series leads it; null when the funnel goes on.</summary>
    public async Task<SpawnResult?> LedAsync(ResolvedProject project, string pipelineName, IncomingTicketEnvelope envelope,
        Dictionary<string, string>? planAnswers, CancellationToken ct)
    {
        if (entry is null || envelope.RequestedByName || planAnswers is { Count: > 0 }
            || !string.Equals(pipelineName, PipelinePresets.CodeName, StringComparison.OrdinalIgnoreCase)) return null;
        var ticketId = envelope.TicketId!;
        if (await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return null;
        if (await queue.FindAsync(project.Name, ticketId, ct) is { } queued)
            return ByName(queued) ? Led(project.Name, ticketId, "its start waits in the capacity queue; the queue starts it") : null;
        try
        {
            return await entry.EnterAsync(project, ticketId, ct) is { } led ? Led(project.Name, ticketId, led) : null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Trigger of {Project}/{Ticket} could not enter its series; nothing was claimed", project.Name, ticketId);
            throw;
        }
    }

    private SpawnResult Led(string project, string ticketId, string led)
    {
        logger.LogInformation("Trigger of {Project}/{Ticket} claims nothing: {Led}", project, ticketId, led);
        return new SpawnResult([ClaimResult.Rejected(ClaimRejectionReason.LedBySeries, led)]);
    }

    // A resume, or a start by name (a person's, the series'): its entry's context is the one the pump has to claim with.
    private static bool ByName(CapacityQueueEntry queued)
    {
        if (queued.IsResume) return true;
        if (queued.InitialContextJson is null) return false;
        var context = JsonSerializer.Deserialize<Dictionary<string, object>>(queued.InitialContextJson);
        return context.IsRequestedByName() || context.IsSeriesAutoStart();
    }
}
