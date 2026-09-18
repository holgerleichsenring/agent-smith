using AgentSmith.Server.Services.SpecDialog;
using Microsoft.AspNetCore.SignalR;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-09-17-042ej: forgets a connection's filed-work watch when the connection goes.
/// <para>
/// A FILTER rather than an <c>OnDisconnectedAsync</c> override on the hub: HubPermissionTests
/// enumerates JobsHub's declared public instance methods and requires each in the permission
/// table, and an override would be one — while not being an invocable method anyone can be
/// authorized for. <see cref="IHubFilter"/> has its own disconnect hook, which is where a
/// connection's leftovers belong.
/// </para>
/// </summary>
internal sealed class FiledWorkDisconnectFilter(FiledWorkWatchRegistry registry) : IHubFilter
{
    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        registry.Forget(context.Context.ConnectionId);
        await next(context, exception);
    }
}
