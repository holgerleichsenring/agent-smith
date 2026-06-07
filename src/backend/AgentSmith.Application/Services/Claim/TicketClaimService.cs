using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// Single entry point for starting a pipeline from a ticket event. Runs the
/// ClaimPreChecker, acquires a per-(platform, ticket) Redis claim-lock, delegates
/// the in-lock lifecycle transition + enqueue to SingleClaimRegionExecutor.
/// One ticket = one enqueue (no per-repo fan-out — unified-run model).
/// </summary>
public sealed class TicketClaimService(
    IRedisClaimLock claimLock,
    ITicketStatusTransitionerFactory transitionerFactory,
    IRedisJobQueue jobQueue,
    IJobHeartbeatService heartbeat,
    IActiveRunLease lease,
    ILogger<TicketClaimService> logger) : ITicketClaimService
{
    private static readonly TimeSpan ClaimLockTtl = TimeSpan.FromSeconds(30);

    public async Task<ClaimResult> ClaimAsync(
        ClaimRequest request, AgentSmithConfig config, CancellationToken ct)
    {
        using var scope = logger.BeginScope("ticket={Ticket}", request.TicketId.Value);

        var rejection = ClaimPreChecker.Check(request, config);
        if (rejection is not null) return LogOne(request, ClaimResult.Rejected(rejection.Value));

        return await ClaimUnderLockAsync(request, config, ct);
    }

    private async Task<ClaimResult> ClaimUnderLockAsync(
        ClaimRequest request, AgentSmithConfig config, CancellationToken ct)
    {
        var lockKey = $"agentsmith:claim-lock:{request.Platform}:{request.TicketId.Value}";
        var token = await claimLock.TryAcquireAsync(lockKey, ClaimLockTtl, ct);
        if (token is null) return LogOne(request, ClaimResult.AlreadyClaimed());

        try
        {
            var tracker = config.Projects[request.ProjectName].Tracker;
            var executor = new SingleClaimRegionExecutor(transitionerFactory, jobQueue, heartbeat, lease, logger);
            return LogOne(request, await executor.ExecuteAsync(request, tracker, ct));
        }
        finally
        {
            await claimLock.ReleaseAsync(lockKey, token, ct);
        }
    }

    private ClaimResult LogOne(ClaimRequest request, ClaimResult result)
    {
        logger.LogInformation(
            "Claim {Outcome} for {Platform}/{Project}/{Ticket} pipeline={Pipeline} rejection={Rejection} error={Error}",
            result.Outcome, request.Platform, request.ProjectName, request.TicketId.Value,
            request.PipelineName, result.Rejection, result.Error);
        return result;
    }
}
