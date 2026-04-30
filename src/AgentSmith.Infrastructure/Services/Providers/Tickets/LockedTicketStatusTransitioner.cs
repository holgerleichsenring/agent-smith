using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Decorator that serializes TransitionAsync via a Redis SETNX lock keyed on the
/// ticket id. Applied only where the underlying API lacks atomic If-Match
/// semantics (Jira labels) AND multiple writers can race in the same composition
/// (Server cluster). ReadCurrentAsync passes through — reads do not need
/// serialization.
/// </summary>
public sealed class LockedTicketStatusTransitioner(
    ITicketStatusTransitioner inner,
    IRedisClaimLock labelLock,
    ILogger<LockedTicketStatusTransitioner> logger) : ITicketStatusTransitioner
{
    private static readonly TimeSpan LabelLockTtl = TimeSpan.FromSeconds(10);

    public string ProviderType => inner.ProviderType;

    public Task<TicketLifecycleStatus?> ReadCurrentAsync(
        TicketId ticketId, CancellationToken cancellationToken)
        => inner.ReadCurrentAsync(ticketId, cancellationToken);

    public async Task<TransitionResult> TransitionAsync(
        TicketId ticketId, TicketLifecycleStatus from,
        TicketLifecycleStatus to, CancellationToken cancellationToken)
    {
        var lockKey = $"agentsmith:jira-label-lock:{ticketId.Value}";
        var token = await labelLock.TryAcquireAsync(lockKey, LabelLockTtl, cancellationToken);
        if (token is null)
        {
            logger.LogWarning(
                "Transition #{Ticket}: label-lock held by another worker", ticketId.Value);
            return TransitionResult.PreconditionFailed("label-lock held");
        }

        try
        {
            return await inner.TransitionAsync(ticketId, from, to, cancellationToken);
        }
        finally
        {
            await labelLock.ReleaseAsync(lockKey, token, cancellationToken);
        }
    }
}
