using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: asks a fresh instance to REFUTE findings nobody vouched for, against the code
/// they point at. Returns null when it could not be asked or answered unreadably —
/// silence is not a verdict, in either direction.
/// </summary>
public interface IFindingRefuter
{
    Task<IReadOnlyList<FindingRefutation>?> RefuteAsync(
        IReadOnlyList<CandidateFinding> candidates,
        AgentConfig agent,
        PipelineCostTracker costTracker,
        CancellationToken cancellationToken);
}
