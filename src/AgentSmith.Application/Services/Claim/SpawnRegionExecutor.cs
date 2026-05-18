using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// Owns the lifecycle-transition + N-enqueue work that happens INSIDE the claim-region
/// lock. Reads the ticket lifecycle once, transitions Pending → Enqueued once, then
/// enqueues each request. Extracted from TicketClaimService to keep both classes under
/// the 120-line limit.
/// </summary>
internal sealed class SpawnRegionExecutor(
    ITicketStatusTransitionerFactory transitionerFactory,
    IRedisJobQueue jobQueue,
    ILogger logger)
{
    public async Task<IReadOnlyList<ClaimResult>> ExecuteAsync(
        IReadOnlyList<ClaimRequest> requests, TrackerConnection tracker, CancellationToken ct)
    {
        var first = requests[0];
        var transitioner = transitionerFactory.Create(tracker);

        var current = await transitioner.ReadCurrentAsync(first.TicketId, ct);
        if (current is not null and not TicketLifecycleStatus.Pending)
            return FillAll(requests, ClaimResult.AlreadyClaimed());

        var transition = await transitioner.TransitionAsync(
            first.TicketId, TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, ct);

        return transition.Outcome switch
        {
            TransitionOutcome.Succeeded => await EnqueueAllAsync(requests, ct),
            TransitionOutcome.PreconditionFailed => FillAll(requests, ClaimResult.AlreadyClaimed()),
            _ => FillAll(requests, ClaimResult.Failed(transition.Error ?? transition.Outcome.ToString()))
        };
    }

    private async Task<IReadOnlyList<ClaimResult>> EnqueueAllAsync(
        IReadOnlyList<ClaimRequest> requests, CancellationToken ct)
    {
        var results = new ClaimResult[requests.Count];
        for (var i = 0; i < requests.Count; i++)
            results[i] = await EnqueueOneAsync(requests[i], ct);
        return results;
    }

    private async Task<ClaimResult> EnqueueOneAsync(ClaimRequest request, CancellationToken ct)
    {
        try
        {
            await jobQueue.EnqueueAsync(ToPipelineRequest(request), ct);
            return ClaimResult.Claimed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Enqueue failed for ticket {Ticket} repo {Repo}",
                request.TicketId.Value, request.RepoName);
            return ClaimResult.Failed($"Enqueue failed: {ex.Message}");
        }
    }

    private static PipelineRequest ToPipelineRequest(ClaimRequest r) => new(
        r.ProjectName, r.PipelineName, TicketId: r.TicketId, Headless: true,
        Context: r.InitialContext, PlanAnswers: r.PlanAnswers, RepoName: r.RepoName);

    private static ClaimResult[] FillAll(IReadOnlyList<ClaimRequest> requests, ClaimResult result)
    {
        var arr = new ClaimResult[requests.Count];
        for (var i = 0; i < arr.Length; i++) arr[i] = result;
        return arr;
    }
}
