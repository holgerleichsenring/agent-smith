using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: what a Request changes starts, the same way on every host. The pull request must
/// come from a ticket branch of the same repository; exactly one configured project whose repository
/// EQUALS the pull request's must have a code run on that ticket; the reviewer must have write access
/// and not be the pull request's author (often our own token). Then the rework entry decides, and the
/// pull request is told — except for an act already served, which a redelivery would otherwise repeat.
/// </summary>
public sealed class PrReworkAdmission(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IConfiguredRepoFinder repoFinder,
    IPreviousAttemptReader attempts,
    IPrReviewAuthorTrust trust,
    IReworkEntry rework,
    ISourceProviderFactory sources,
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
        if (!await trust.IsTrustedAsync(request.Host, request.Reviewer, ct))
            return WebhookResult.NotHandled("the reviewer may not write to the repository");
        var outcome = await rework.EnterAsync(owner.Project, ticketId,
            request.Act with { Channel = ReworkChannel.PullRequest }, PipelinePresets.CodeName, ct);
        if (Text(request, ticketId, outcome) is { } text) await SayAsync(owner, request.PrNumber, text, ct);
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

    private static string? Text(PrReviewRequest request, string ticketId, ReworkOutcome outcome) => outcome.Kind switch
    {
        ReworkOutcomeKind.Started => ReworkTexts.PrStarted(ticketId, outcome.RunId),
        ReworkOutcomeKind.Refused when outcome.Busy
            => ReworkTexts.PrLiveRun(ticketId, outcome.RunId ?? "(starting)", request.Host == RepoType.AzureDevOps),
        ReworkOutcomeKind.Refused => ReworkTexts.PrRefused(ticketId, outcome.Reason!),
        ReworkOutcomeKind.NotARework => ReworkTexts.PrRefused(ticketId, "the ticket is not waiting for one — its last run has not finished, or it is parked as not implementable"),
        _ => null,
    };

    private async Task SayAsync(ConfiguredRepo owner, string prNumber, string text, CancellationToken ct)
    {
        try
        {
            if (sources.Create(owner.Repo) is IPrCommentProvider comments) await comments.PostCommentAsync(prNumber, text, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not answer the review on {Repo}#{Pr}", owner.Repo.Name, prNumber);
        }
    }
}
