using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Triage;

/// <summary>p0391: resolves the native ticket status a clarification-halted run parks in.</summary>
public interface IClarificationParkStatusResolver
{
    /// <summary>
    /// The status to park in, or null when none is configured. p0391a: null is the run's
    /// answer, not the process's — the step fails with the reason and the ticket keeps its
    /// current status, rather than the run posting its question and ending while the ticket
    /// stays claimable.
    /// </summary>
    string? TryResolve(PipelineContext pipeline, TrackerConnection tracker);

    /// <summary>The operator-language reason a run cannot park, used as the failure reason.</summary>
    string UnresolvedReason { get; }
}
