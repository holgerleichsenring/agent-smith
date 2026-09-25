using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: the hub method's own work — the owner check, the filing read and the
/// registration — held off the hub so the hub stays the transport it is.
/// <para>
/// THE SERVER CHOOSES THE TICKET IDS. The call accepts a dialog id and nothing else; the ids
/// come from the open session's latest filing, so a caller cannot follow a ticket by naming it,
/// and an id with no open session watches nothing — the same answer the read gives.
/// </para>
/// </summary>
public sealed class FiledWorkWatch(
    SpecDialogOwnership ownership,
    FiledWorkFiling filing,
    FiledWorkBoundTicket bound,
    FiledWorkWatchRegistry registry)
{
    /// <summary>
    /// Registers this connection for what the dialog's session is following. Throws for a dialog
    /// belonging to another principal: a browser-minted dialog id is no boundary, and this is
    /// the surface that follows real runs.
    /// </summary>
    public async Task WatchAsync(HubCallerContext context, string dialogId)
    {
        ArgumentNullException.ThrowIfNull(context);
        var owner = ownership.OwnerOf(context.User);
        if (!await ownership.MayWatchAsync(dialogId, owner, context.ConnectionAborted))
            throw new HubException($"Spec dialog '{dialogId}' belongs to another principal.");
        registry.Watch(
            context.ConnectionId, await FollowedAsync(dialogId, context.ConnectionAborted));
    }

    /// <summary>
    /// 2026-09-25-c4a6: the ids the read draws rows for — the latest filing's, and the BOUND
    /// ticket's when the filing named none. A bound conversation registered nothing, so a run of
    /// its ticket could finish with the pane still showing the state it was fetched in. Read
    /// server-side off the session row like the filing above it, so the caller still names a
    /// dialog id and never a ticket.
    /// </summary>
    private async Task<IReadOnlyList<string>> FollowedAsync(string dialogId, CancellationToken ct)
    {
        var filed = await filing.TicketIdsAsync(dialogId, ct);
        if (filed.Count > 0) return filed;
        return await bound.OfAsync(dialogId, ct) is { } ticket ? [ticket.TicketId] : [];
    }
}
