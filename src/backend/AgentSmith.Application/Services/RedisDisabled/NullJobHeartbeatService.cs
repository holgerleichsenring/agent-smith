using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.RedisDisabled;

/// <summary>
/// Fallback IJobHeartbeatService for Redis-less CLI runs. Manual pipelines without a
/// TicketId never start a heartbeat (LifecycleScope.Noop); pipelines that do try fail
/// fast on Start with a clear message. IsAliveAsync returns false — no heartbeat means
/// no ownership claim from this process.
/// </summary>
public sealed class NullJobHeartbeatService : IJobHeartbeatService
{
    public IAsyncDisposable Start(TicketId ticketId)
        => throw new RedisUnavailableException("Job heartbeat");

    public Task<bool> IsAliveAsync(TicketId ticketId, CancellationToken cancellationToken)
        => Task.FromResult(false);
}
