using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429: a refuter whose answer the test dictates. A null answer is the refuter that
/// could not be asked — the case where every candidate must survive untouched, because
/// silence is not a verdict in either direction.
/// </summary>
internal sealed class ScriptedRefuter(IReadOnlyList<FindingRefutation>? answers) : IFindingRefuter
{
    public IReadOnlyList<CandidateFinding> Asked { get; private set; } = [];

    public Task<IReadOnlyList<FindingRefutation>?> RefuteAsync(
        IReadOnlyList<CandidateFinding> candidates,
        AgentConfig agent,
        PipelineCostTracker costTracker,
        CancellationToken cancellationToken)
    {
        Asked = candidates;
        return Task.FromResult(answers);
    }
}
