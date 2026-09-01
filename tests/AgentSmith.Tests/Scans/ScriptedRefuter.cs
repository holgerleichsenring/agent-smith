using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.Scans;

/// <summary>
/// p0429: a refuter whose answer the test dictates. A null answer is the refuter that
/// could not be asked — the case where every candidate must survive untouched, because
/// silence is not a verdict in either direction.
/// <para>
/// 2026-09-01-85b2: an answer may also be computed FROM the candidates, because a verdict
/// now echoes the id the call stamped on the finding it is about, and a test that hard-codes
/// those ids is pinning the stamping rather than the routing.
/// </para>
/// </summary>
internal sealed class ScriptedRefuter(
    Func<IReadOnlyList<CandidateFinding>, IReadOnlyList<FindingRefutation>?> answer) : IFindingRefuter
{
    internal ScriptedRefuter(IReadOnlyList<FindingRefutation> answers) : this(_ => answers) { }

    /// <summary>The refuter that could not be asked, or answered unreadably.</summary>
    internal static ScriptedRefuter Unreachable() => new(_ => null);

    public IReadOnlyList<CandidateFinding> Asked { get; private set; } = [];

    public Task<IReadOnlyList<FindingRefutation>?> RefuteAsync(
        IReadOnlyList<CandidateFinding> candidates,
        AgentConfig agent,
        PipelineCostTracker costTracker,
        CancellationToken cancellationToken)
    {
        Asked = candidates;
        return Task.FromResult(answer(candidates));
    }
}
