using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs;

/// <summary>
/// 2026-11-23-933da: the pocs switch. As an endpoint filter on the /api/pocs group — so it runs after authorization —
/// it answers an empty 404 on every PoC route while pocs.enabled is off; every PoC hosted service asks
/// <see cref="Enabled"/> on each pass and does nothing while it is off.
/// </summary>
public sealed class PocsGate(TicketProjects projects) : IEndpointFilter
{
    public bool Enabled(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Pocs.Enabled;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return Enabled(projects.Config()) ? await next(context) : Results.NotFound();
    }
}
