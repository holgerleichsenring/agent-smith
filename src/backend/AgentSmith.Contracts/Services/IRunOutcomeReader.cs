using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>Reads how a run stands from its record; null when there is no record of the run.</summary>
public interface IRunOutcomeReader
{
    Task<RunOutcome?> ReadAsync(string runId, CancellationToken cancellationToken);
}
