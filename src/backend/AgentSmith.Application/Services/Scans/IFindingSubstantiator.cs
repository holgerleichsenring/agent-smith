using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the delivered finding set after every unvouched finding has been put to a
/// fresh instance — invented citations dropped, refuted claims downgraded.
/// </summary>
public interface IFindingSubstantiator
{
    Task<IReadOnlyList<SkillObservation>> SubstantiateAsync(
        PipelineContext pipeline, CancellationToken cancellationToken);
}
