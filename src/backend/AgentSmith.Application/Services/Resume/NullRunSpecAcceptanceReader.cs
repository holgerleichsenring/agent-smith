using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Resume;

/// <summary>2026-11-18-550ab: the DB-free default — no run history, so no stored acceptance.</summary>
public sealed class NullRunSpecAcceptanceReader : IRunSpecAcceptanceReader
{
    public Task<AcceptanceView?> ReadAsync(string runId, string specId, CancellationToken cancellationToken) =>
        Task.FromResult<AcceptanceView?>(null);
}
