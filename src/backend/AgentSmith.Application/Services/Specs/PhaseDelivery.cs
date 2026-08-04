using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0400: the current phase's delivery declaration. A ratified spec may state
/// <c>ships_code: false</c> — the phase ships knowledge (inventory,
/// classification, analysis) instead of a source change, and keystone and
/// build gate judge it by its done criteria. Absent spec or absent field
/// means the default: the phase ships code. Same source the acceptance
/// criteria are read from, so the two verdict inputs cannot diverge.
/// </summary>
public static class PhaseDelivery
{
    public static bool ShipsCode(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return !pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft)
            || draft is null || draft.ShipsCode;
    }
}
