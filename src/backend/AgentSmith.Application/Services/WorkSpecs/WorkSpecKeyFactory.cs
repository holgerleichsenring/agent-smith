using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: resolves the work-spec key for a run. The provider half comes from the
/// tracker platform the run was configured with; the ticket's own Source is the
/// fallback for runs (inline tickets, tests) that never set the platform key.
/// </summary>
public static class WorkSpecKeyFactory
{
    public static WorkSpecKey For(Ticket ticket, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var platform = pipeline.TryGet<string>(ContextKeys.TrackerPlatform, out var p)
            && !string.IsNullOrWhiteSpace(p) ? p! : ticket.Source;
        return WorkSpecKey.For(platform, ticket.Id.Value);
    }
}
