using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// Fallback ITicketClaimService used by the CLI server when Redis is unavailable.
/// Every claim is reported as Failed("redis_unavailable") so callers (WebhookRequestProcessor,
/// PollerHostedService) can distinguish 'queue missing' from real platform-side rejections and
/// answer the operator with a 503. Logs once per process to avoid log floods.
/// </summary>
public sealed class NullTicketClaimService(ILogger<NullTicketClaimService> logger) : ITicketClaimService
{
    private const string RedisUnavailable = "redis_unavailable";
    private int _logged;

    public Task<ClaimResult> ClaimAsync(
        ClaimRequest request, AgentSmithConfig config, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _logged, 1) == 0)
        {
            logger.LogWarning(
                "ClaimAsync called but Redis is not configured — server is up but cannot enqueue jobs. Set REDIS_URL.");
        }
        return Task.FromResult(ClaimResult.Failed(RedisUnavailable));
    }
}
