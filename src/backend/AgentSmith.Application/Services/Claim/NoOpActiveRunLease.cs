using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// The DB-free binding (CLI single-binary, or a server with persistence off):
/// the relational lease is absent, so claims always succeed here and the other
/// guards (the Redis heartbeat / status transition) remain the single-run
/// barrier. Keeps the claim path working with no DB dependency.
/// </summary>
public sealed class NoOpActiveRunLease : IActiveRunLease
{
    public Task<LeaseClaimOutcome> TryClaimAsync(string project, TicketId ticketId, CancellationToken cancellationToken)
        => Task.FromResult(LeaseClaimOutcome.Claimed);

    public Task ReleaseAsync(string project, TicketId ticketId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task AttachRunAsync(string project, TicketId ticketId, string runId, string? jobId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task RenewHeartbeatAsync(string project, TicketId ticketId, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<IReadOnlyList<StaleLease>> FindStaleAsync(TimeSpan olderThan, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StaleLease>>([]);

    public Task<StaleLease?> GetByTicketAsync(string project, TicketId ticketId, CancellationToken cancellationToken)
        => Task.FromResult<StaleLease?>(null);

    public Task<IReadOnlyCollection<string>> GetActiveRunIdsAsync(TimeSpan freshFor, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<string>>([]);
}
