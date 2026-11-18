using AgentSmith.Contracts.Runs;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-18-550ab: the acceptance a run stored for one of its specs — the last account taken of it, as the run detail
/// renders it. Null when the run stored none for the spec, or the composition has no database.
/// </summary>
public interface IRunSpecAcceptanceReader
{
    Task<AcceptanceView?> ReadAsync(string runId, string specId, CancellationToken cancellationToken);
}
