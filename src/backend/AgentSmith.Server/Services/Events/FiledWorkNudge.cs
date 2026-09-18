using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-09-17-042ej: tells the connections following a work ticket that something about it
/// moved. A DATA-FREE nudge, as the run list's own is: the page refetches its filed work over
/// the read that checks ownership, so nothing about a run travels through this channel and no
/// caller is joined to a run's group.
/// <para>
/// A nudge cannot fire before the run knows its ticket — the snapshot's TicketId is null until
/// FetchTicket lands on the stream — so the page's own fetch on connect and after a filing is
/// what shows that first state, and the nudge takes over from there.
/// </para>
/// </summary>
public sealed class FiledWorkNudge(IHubContext<JobsHub> hub, FiledWorkWatchRegistry registry)
{
    /// <summary>The message name the dashboard listens on.</summary>
    public const string Message = "FiledWorkChanged";

    public Task OfAsync(RunSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var connections = registry.Watching(snapshot.TicketId);
        return connections.Count == 0
            ? Task.CompletedTask
            : hub.Clients.Clients(connections).SendCoreAsync(Message, [], cancellationToken);
    }
}
