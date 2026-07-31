using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: which native status a hand-back parks in. The two question cases park
/// where p0318 already parks; a NOT-IMPLEMENTABLE verdict parks in its own
/// status when one is configured, and degrades to the clarification park when it
/// is not — a park in the wrong status is still a park, whereas no park at all
/// leaves the ticket claimable and the run repeats indefinitely.
/// </summary>
public sealed class WorkSpecParkStatusResolver(IClarificationParkStatusResolver clarification)
{
    public string Resolve(
        PipelineContext pipeline, TrackerConnection tracker, WorkSpecHandbackCase handbackCase)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        if (handbackCase != WorkSpecHandbackCase.NotImplementable)
            return clarification.Resolve(pipeline, tracker);
        if (pipeline.TryGet<string>(ContextKeys.NotImplementableStatus, out var seeded)
            && !string.IsNullOrWhiteSpace(seeded))
            return seeded!;
        return string.IsNullOrWhiteSpace(tracker.NotImplementableStatus)
            ? clarification.Resolve(pipeline, tracker)
            : tracker.NotImplementableStatus;
    }
}
