using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// Single entry point for starting a pipeline from a ticket event.
/// Runs pre-checks, acquires a Redis claim lock, transitions ticket Pending → Enqueued,
/// and enqueues a PipelineRequest. Status stays Enqueued on enqueue failure — recovery
/// via EnqueuedReconciler in p95c.
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
        var rejection = ClaimPreChecker.Check(request, config);
        if (rejection is not null) return Log(request, ClaimResult.Rejected(rejection.Value));

        var lockKey = $"agentsmith:claim-lock:{request.Platform}:{request.TicketId.Value}";
        var token = await claimLock.TryAcquireAsync(lockKey, ClaimLockTtl, ct);
        if (token is null) return Log(request, ClaimResult.AlreadyClaimed());

        try
        {
            var ticketConfig = config.Projects[request.ProjectName].Tickets;
            return await AttemptClaimAsync(request, ticketConfig, ct);
        }
        finally
        {
            await claimLock.ReleaseAsync(lockKey, token, ct);
        }
    }

    private async Task<ClaimResult> AttemptClaimAsync(
        ClaimRequest request, TicketConfig ticketConfig, CancellationToken ct)
    {
        var transitioner = transitionerFactory.Create(ticketConfig);

        var current = await transitioner.ReadCurrentAsync(request.TicketId, ct);
        if (current is not null and not TicketLifecycleStatus.Pending)
            return Log(request, ClaimResult.AlreadyClaimed());

        var transition = await transitioner.TransitionAsync(
            request.TicketId, TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, ct);

        return transition.Outcome switch
        {
            TransitionOutcome.Succeeded => await EnqueueAsync(request, ct),
            TransitionOutcome.PreconditionFailed => Log(request, ClaimResult.AlreadyClaimed()),
            _ => Log(request, ClaimResult.Failed(transition.Error ?? transition.Outcome.ToString()))
        };
    }

    private async Task<ClaimResult> EnqueueAsync(ClaimRequest request, CancellationToken ct)
    {
        var pipelineRequest = new PipelineRequest(
            request.ProjectName,
            request.PipelineName,
            TicketId: request.TicketId,
            Headless: true,
            Context: request.InitialContext);

        try
        {
            await jobQueue.EnqueueAsync(pipelineRequest, ct);
            return Log(request, ClaimResult.Claimed());
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Enqueue failed for {Ticket} — status stays Enqueued, reconciler will recover",
                request.TicketId.Value);
            return ClaimResult.Failed($"Enqueue failed: {ex.Message}");
        }
    }

    private ClaimResult Log(ClaimRequest request, ClaimResult result)
    {
        logger.LogInformation(
            "Claim {Outcome} for {Platform}/{Project}/{Ticket} pipeline={Pipeline} rejection={Rejection} error={Error}",
            result.Outcome, request.Platform, request.ProjectName, request.TicketId.Value,
            request.PipelineName, result.Rejection, result.Error);
        return result;
    }
}
