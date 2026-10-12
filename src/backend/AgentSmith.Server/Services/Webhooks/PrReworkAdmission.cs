using AgentSmith.Application.Services;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: what a Request changes starts, the same way on every host. The pull request must
/// come from a ticket branch of the same repository; exactly one configured project whose repository
/// EQUALS the pull request's must have a code run on that ticket; the reviewer must not be the pull
/// request's author (often our own token).
/// <para>2026-10-08-0781: then the ticket is nudged and the delivery answered — no host call in the
/// request. The worker reads the act from the host, with the host's trust, and answers it.</para>
/// </summary>
public sealed class PrReworkAdmission(IPrTicketOwner owners, IReworkNudges nudges)
{
    public async Task<WebhookResult> AdmitAsync(PrReviewRequest request, CancellationToken ct)
    {
        if (TicketBranchNamer.TicketOf(request.HeadRef) is not { } ticketId)
            return WebhookResult.NotHandled("the pull request is not from an agent-smith ticket branch");
        if (!request.SameRepository) return WebhookResult.NotHandled("the pull request comes from a fork");
        if (string.Equals(request.Reviewer.AuthorId, request.PrAuthorId, StringComparison.OrdinalIgnoreCase))
            return WebhookResult.NotHandled("the reviewer is the pull request's author");
        if (await owners.FindAsync(request.RepoUrl, ticketId, null, ct) is not { } owner)
            return WebhookResult.NotHandled($"no single configured project has a run on ticket {ticketId}");
        await nudges.EnqueueAsync(new ReworkNudgeRequest(owner.ProjectName, ticketId, ReworkNudgeOrigin.PullRequest,
            request.PrUrl, ReworkChannel.PullRequest), ct);
        return WebhookResult.HandledNoRoute();
    }
}
