using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-10-08-e8b9c: tells the pull request a Request changes came from that its specification is full
/// — every one of SpecSet.MaxPhases phases has run — so the review was not cut into it.
/// </summary>
public interface IFullSetPrNotice
{
    Task PostAsync(PipelineContext pipeline, SpecSet set, CancellationToken cancellationToken);
}
