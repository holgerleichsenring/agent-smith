using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-02-5ab2b: the production start gate over a test's cancel flag, claim and heartbeat —
/// the DB-free bindings where a test does not care.
/// </summary>
internal static class TestRunStartGate
{
    internal static RunStartGate Over(
        IRunCancelStateReader cancelState, IRunStartClaim? claim = null,
        IRunHeartbeat? heartbeat = null, TimeProvider? clock = null) => new(
        claim ?? new NoOpRunStartClaim(), cancelState,
        new RunHeartbeatPump(new NoOpActiveRunLease(), heartbeat ?? new NoOpRunHeartbeat(),
            clock ?? TimeProvider.System, NullLogger<RunHeartbeatPump>.Instance),
        NullLogger<RunStartGate>.Instance);
}
