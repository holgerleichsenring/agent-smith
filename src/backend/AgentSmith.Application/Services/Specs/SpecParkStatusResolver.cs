using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: which native status a hand-back parks in. The contradiction case parks
/// where p0318 already parks; a NOT-IMPLEMENTABLE verdict parks in its own status
/// when one is configured, and degrades to the clarification park when it is not —
/// a park in the wrong status is still a park, whereas no park at all leaves the
/// ticket claimable and the run repeats indefinitely.
/// </summary>
public sealed class SpecParkStatusResolver(IClarificationParkStatusResolver clarification)
{
    /// <summary>
    /// The status to park in, or null when none is configured. p0391a: null is the run's
    /// answer, not the process's — the step fails with <see cref="UnresolvedReason"/> and
    /// the ticket keeps its current status, rather than the run handing back while the
    /// ticket stays claimable.
    /// </summary>
    public string? TryResolve(
        PipelineContext pipeline, TrackerConnection tracker, SpecHandbackCase handbackCase)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        if (handbackCase != SpecHandbackCase.NotImplementable)
            return clarification.TryResolve(pipeline, tracker);
        if (pipeline.TryGet<string>(ContextKeys.NotImplementableStatus, out var seeded)
            && !string.IsNullOrWhiteSpace(seeded))
            return seeded!;
        return string.IsNullOrWhiteSpace(tracker.NotImplementableStatus)
            ? clarification.TryResolve(pipeline, tracker)
            : tracker.NotImplementableStatus;
    }

    /// <summary>The operator-language reason a hand-back cannot park, used as the failure reason.</summary>
    public string UnresolvedReason => clarification.UnresolvedReason;
}
