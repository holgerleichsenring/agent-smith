using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Triage;

/// <summary>p0391: resolves the native ticket status a clarification-halted run parks in.</summary>
public interface IClarificationParkStatusResolver
{
    /// <summary>
    /// The status to park in. Throws when none is configured — a run that cannot park would
    /// post its question and end while the ticket stays claimable.
    /// </summary>
    string Resolve(PipelineContext pipeline, TrackerConnection tracker);
}
