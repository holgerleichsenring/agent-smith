using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// p0391: resolves the native status a clarification-halted ticket parks in — the trigger's
/// seeded value first (the ticket-triggered funnel publishes it into the run context), then the
/// tracker's own base value (the CLI path builds no trigger).
///
/// p0391a: nothing configured fails THE RUN, not the process. ClarificationParkStatusRule
/// already records a blocking finding for this configuration and the trigger it names is not
/// started, so reaching here means the run would otherwise post a question and end with the
/// ticket still claimable — an unbounded re-trigger loop.
/// </summary>
public sealed class ClarificationParkStatusResolver : IClarificationParkStatusResolver
{
    public string UnresolvedReason =>
        "This run must park on an operator question, but needs_clarification_status is not "
        + "configured for its tracker. Set needs_clarification_status (tracker base or the "
        + "project's trigger) to a status OUTSIDE trigger_statuses — without it the question "
        + "is posted while the ticket stays claimable and the run repeats indefinitely.";

    public string? TryResolve(PipelineContext pipeline, TrackerConnection tracker)
    {
        if (pipeline.TryGet<string>(ContextKeys.NeedsClarificationStatus, out var seeded)
            && !string.IsNullOrWhiteSpace(seeded))
            return seeded;
        return string.IsNullOrWhiteSpace(tracker.NeedsClarificationStatus)
            ? null
            : tracker.NeedsClarificationStatus;
    }
}
