using AgentSmith.Contracts.Series;
using AgentSmith.Server.Services.ChatLaunch;

namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-11-26-5f6e: a start's outcome, read from what the run-start path answered: a claimed run is started, a queued
/// one queued, anything else refused with the claim's words. 2026-11-27-3532a: read from the launch
/// (<see cref="ChatTicketRunLauncher"/>) itself — a queued start says its place in the queue and why it waits, and a ticket
/// another claimer took first is started with the run its lease names.</summary>
internal static class SpawnedStart
{
    /// <param name="place">The run's 1-based place in the capacity queue; 0 when it is not known.</param>
    /// <param name="leasedRunId">The run the ticket's lease names when another claimer took it first; null otherwise.</param>
    public static SeriesStart Of(string ticketId, ChatLaunchResult launch, int place, string? leasedRunId)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (launch is { WaitReason: { } reason, RunId: { } queued })
            return new(SeriesStartKind.Queued, $"Ticket {ticketId} is queued as run {queued}{Place(place)}: {reason}; it starts once there is room.",
                queued);
        if (launch.RunId is { } run) return new(SeriesStartKind.Started, $"Ticket {ticketId} started: run {run}.", run);
        if (launch.TakenByAnother)
            return leasedRunId is not null
                ? new(SeriesStartKind.Started, $"Ticket {ticketId} started: run {leasedRunId}.", leasedRunId)
                : SeriesStart.Refused($"Ticket {ticketId} was not started: a run holds it already; retry or tell it instead.");
        return SeriesStart.Refused($"Ticket {ticketId} was not started: {launch.Refusal?.TrimEnd('.') ?? "the run could not be started"}.");
    }

    private static string Place(int place) => place > 0 ? $" (place {place} in the capacity queue)" : string.Empty;
}
