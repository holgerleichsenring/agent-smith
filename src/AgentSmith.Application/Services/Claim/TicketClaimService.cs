using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// Single entry point for starting a pipeline from a ticket event. ClaimSpawnAsync is
/// the claim-region method for multi-repo spawn: one pre-check + one lock + one lifecycle
/// transition + N enqueues (delegated to SpawnRegionExecutor). The single-request
/// ClaimAsync is preserved as a 1-element wrapper for callers that don't fan out.
/// </summary>
public sealed class TicketClaimService(
    IRedisClaimLock claimLock,
    ITicketStatusTransitionerFactory transitionerFactory,
    IRedisJobQueue jobQueue,
    ILogger<TicketClaimService> logger) : ITicketClaimService
{
    private static readonly TimeSpan ClaimLockTtl = TimeSpan.FromSeconds(30);

    public async Task<ClaimResult> ClaimAsync(
        ClaimRequest request, AgentSmithConfig config, CancellationToken ct)
    {
        var results = await ClaimSpawnAsync(new[] { request }, config, ct);
        return results[0];
    }

    public async Task<IReadOnlyList<ClaimResult>> ClaimSpawnAsync(
        IReadOnlyList<ClaimRequest> requests, AgentSmithConfig config, CancellationToken ct)
    {
        if (requests.Count == 0) return Array.Empty<ClaimResult>();
        AssertSharedTicket(requests);
        var first = requests[0];
        using var scope = logger.BeginScope("ticket={Ticket}", first.TicketId.Value);

        var rejection = PreCheckAll(requests, config);
        if (rejection is not null)
            return LogAll(requests, ClaimResult.Rejected(rejection.Value));

        return await ClaimUnderLockAsync(requests, config, ct);
    }

    private async Task<IReadOnlyList<ClaimResult>> ClaimUnderLockAsync(
        IReadOnlyList<ClaimRequest> requests, AgentSmithConfig config, CancellationToken ct)
    {
        var first = requests[0];
        var lockKey = $"agentsmith:claim-lock:{first.Platform}:{first.TicketId.Value}";
        var token = await claimLock.TryAcquireAsync(lockKey, ClaimLockTtl, ct);
        if (token is null) return LogAll(requests, ClaimResult.AlreadyClaimed());

        try
        {
            var tracker = config.Projects[first.ProjectName].Tracker;
            var executor = new SpawnRegionExecutor(transitionerFactory, jobQueue, logger);
            var results = await executor.ExecuteAsync(requests, tracker, ct);
            return LogPerRequest(requests, results);
        }
        finally
        {
            await claimLock.ReleaseAsync(lockKey, token, ct);
        }
    }

    private static ClaimRejectionReason? PreCheckAll(
        IReadOnlyList<ClaimRequest> requests, AgentSmithConfig config)
    {
        foreach (var req in requests)
        {
            var rejection = ClaimPreChecker.Check(req, config);
            if (rejection is not null) return rejection;
        }
        return null;
    }

    private IReadOnlyList<ClaimResult> LogAll(IReadOnlyList<ClaimRequest> requests, ClaimResult result)
    {
        var arr = new ClaimResult[requests.Count];
        for (var i = 0; i < requests.Count; i++) arr[i] = LogOne(requests[i], result);
        return arr;
    }

    private IReadOnlyList<ClaimResult> LogPerRequest(
        IReadOnlyList<ClaimRequest> requests, IReadOnlyList<ClaimResult> results)
    {
        for (var i = 0; i < requests.Count; i++) LogOne(requests[i], results[i]);
        return results;
    }

    private ClaimResult LogOne(ClaimRequest request, ClaimResult result)
    {
        logger.LogInformation(
            "Claim {Outcome} for {Platform}/{Project}/{Ticket} repo={Repo} pipeline={Pipeline} rejection={Rejection} error={Error}",
            result.Outcome, request.Platform, request.ProjectName, request.TicketId.Value,
            request.RepoName, request.PipelineName, result.Rejection, result.Error);
        return result;
    }

    private static void AssertSharedTicket(IReadOnlyList<ClaimRequest> requests)
    {
        var first = requests[0];
        for (var i = 1; i < requests.Count; i++)
            if (requests[i].Platform != first.Platform
                || !requests[i].TicketId.Value.Equals(first.TicketId.Value, StringComparison.Ordinal))
                throw new ArgumentException("ClaimSpawnAsync requires shared Platform + TicketId.");
    }
}
