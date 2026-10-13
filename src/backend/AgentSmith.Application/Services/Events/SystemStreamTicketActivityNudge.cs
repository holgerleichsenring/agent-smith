using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// 2026-10-13-9a59a: the one ticket nudge — a <see cref="TicketActivityChangedEvent"/> on the
/// system stream. Safe in every composition: the system publisher is a no-op outside the server.
/// </summary>
public sealed class SystemStreamTicketActivityNudge(
    ISystemEventPublisher publisher, TimeProvider clock) : ITicketActivityNudge
{
    public const string Source = "ticket-activity";

    public Task ChangedAsync(string project, string ticketKey, CancellationToken cancellationToken) =>
        publisher.PublishAsync(
            new TicketActivityChangedEvent(Source, project, ticketKey, clock.GetUtcNow()), cancellationToken);
}
