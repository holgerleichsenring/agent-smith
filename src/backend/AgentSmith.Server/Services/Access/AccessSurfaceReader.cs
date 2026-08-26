using AgentSmith.Contracts.Models.Access;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-08-26-7a51: the access surface's one read. The observed callers are the pickable
/// half and the mapping is the decided half; an unreachable observation store costs the
/// surface its pick list, never its ability to grant a role to a value typed by hand.
/// </summary>
internal sealed class AccessSurfaceReader(
    RoleMappingSource mapping,
    IObservedCallerStore observed,
    AccessViewComposer composer,
    ILogger<AccessSurfaceReader> logger)
{
    public async Task<AccessView> ViewAsync(CancellationToken ct) =>
        composer.Compose(mapping.Current(), await ObservedAsync(ct));

    private async Task<IReadOnlyList<ObservedCaller>> ObservedAsync(CancellationToken ct)
    {
        try
        {
            return await observed.AllAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The panes still render from the mapping alone: what is DECIDED is the half an
            // administrator came here to change, and it does not live in this store.
            logger.LogWarning(ex, "The observed callers could not be read for the access surface");
            return [];
        }
    }
}
