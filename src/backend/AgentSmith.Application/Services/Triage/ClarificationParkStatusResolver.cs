using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Application.Services.Triage;

/// <summary>
/// p0391: resolves the native status a clarification-halted ticket parks in — the trigger's
/// seeded value first (the ticket-triggered funnel publishes it into the run context), then the
/// tracker's own base value (the CLI path builds no trigger). Nothing configured is a hard
/// error, not a log line: <see cref="ClarificationParkStatusValidator"/> rejects that config at
/// load, so reaching here means the run would otherwise post a question and end with the ticket
/// still claimable — an unbounded re-trigger loop.
/// </summary>
public sealed class ClarificationParkStatusResolver : IClarificationParkStatusResolver
{
    public string Resolve(PipelineContext pipeline, TrackerConnection tracker)
    {
        if (pipeline.TryGet<string>(ContextKeys.NeedsClarificationStatus, out var seeded)
            && !string.IsNullOrWhiteSpace(seeded))
            return seeded;
        if (!string.IsNullOrWhiteSpace(tracker.NeedsClarificationStatus))
            return tracker.NeedsClarificationStatus;

        throw new ConfigurationException(
            "This run must park on an operator question, but needs_clarification_status is not "
            + "configured for its tracker. Set needs_clarification_status (tracker base or the "
            + "project's trigger) to a status OUTSIDE trigger_statuses — without it the question "
            + "is posted while the ticket stays claimable and the run repeats indefinitely.");
    }
}
