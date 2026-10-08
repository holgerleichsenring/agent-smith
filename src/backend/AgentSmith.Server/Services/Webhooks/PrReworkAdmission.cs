using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: what a Request changes starts, the same way on every host. The pull request must
/// come from a ticket branch of the same repository; exactly one configured project whose repository
/// EQUALS the pull request's must have a code run on that ticket; the reviewer must not be the pull
/// request's author (often our own token).
/// <para>2026-10-08-0781: then the ticket is nudged and the delivery answered — no host call in the
/// request. The worker reads the act from the host, with the host's trust, and answers it.</para>
/// </summary>
public sealed class PrReworkAdmission(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IConfiguredRepoFinder repoFinder,
    IPreviousAttemptReader attempts,
    IReworkNudges nudges,
    ILogger<PrReworkAdmission> logger)
{
    public async Task<WebhookResult> AdmitAsync(PrReviewRequest request, CancellationToken ct)
    {
        if (TicketBranchNamer.TicketOf(request.HeadRef) is not { } ticketId)
            return WebhookResult.NotHandled("the pull request is not from an agent-smith ticket branch");
        if (!request.SameRepository) return WebhookResult.NotHandled("the pull request comes from a fork");
        if (string.Equals(request.Reviewer.AuthorId, request.PrAuthorId, StringComparison.OrdinalIgnoreCase))
            return WebhookResult.NotHandled("the reviewer is the pull request's author");
        if (await OwnerAsync(request.RepoUrl, ticketId, ct) is not { } owner)
            return WebhookResult.NotHandled($"no single configured project has a run on ticket {ticketId}");
        await nudges.EnqueueAsync(new ReworkNudgeRequest(owner.ProjectName, ticketId, ReworkNudgeOrigin.PullRequest,
            request.PrUrl, ReworkChannel.PullRequest), ct);
        return WebhookResult.HandledNoRoute();
    }

    // Equal repositories only, and of those the projects that actually ran this ticket: issue numbers
    // are per repository, so two projects on one repository could both own a #12.
    private async Task<ConfiguredRepo?> OwnerAsync(string repoUrl, string ticketId, CancellationToken ct)
    {
        var owners = new List<ConfiguredRepo>();
        foreach (var candidate in repoFinder.FindAll(configLoader.LoadConfig(serverContext.ConfigPath), repoUrl))
            if (await attempts.LatestAsync(candidate.ProjectName, ticketId, null, ct) is not null) owners.Add(candidate);
        if (owners.Count != 1)
            logger.LogInformation("Request changes on {Repo} for ticket {Ticket}: {Count} owning project(s) — not handled",
                repoUrl, ticketId, owners.Count);
        return owners.Count == 1 ? owners[0] : null;
    }
}
