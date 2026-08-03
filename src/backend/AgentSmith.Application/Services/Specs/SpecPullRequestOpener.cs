using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: find-or-create at the spec commit. Always a DRAFT — the run is still
/// working and no phase has been verified yet; CommitAndPR finds this pull request,
/// refreshes its body with the phase-status table and promotes it to ready ONLY when
/// the whole sequence is through. Failure is logged and swallowed: the specs are on
/// the branch either way, and the run must not die because a PR could not be opened.
/// </summary>
public sealed class SpecPullRequestOpener(
    ISourceProviderFactory sourceFactory,
    IEventPublisher events,
    ILogger<SpecPullRequestOpener> logger) : ISpecPullRequestOpener
{
    public async Task<string?> OpenAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carryingRepo);
        ArgumentNullException.ThrowIfNull(set);
        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) || repository is null)
            return null;
        var branchRepo = new Repository(repository.CurrentBranch, carryingRepo.Url ?? string.Empty);
        try
        {
            var provider = sourceFactory.Create(carryingRepo);
            var url = await provider.FindOpenPullRequestAsync(branchRepo, cancellationToken)
                ?? await CreateAsync(provider, branchRepo, pipeline, set, cancellationToken);
            await RecordAsync(pipeline, carryingRepo.Name, url, cancellationToken);
            return url;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "{Repo}: could not open the draft PR at the spec commit", carryingRepo.Name);
            return null;
        }
    }

    private async Task<string> CreateAsync(
        ISourceProvider provider, Repository branchRepo, PipelineContext pipeline,
        SpecSet set, CancellationToken ct)
    {
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        var url = await provider.CreatePullRequestAsync(
            branchRepo, ticket?.Title ?? set.Key,
            SpecPrBody.BuildInitial(set), ct, linkedTicketId: ticket?.Id, isDraft: true);
        logger.LogInformation("Draft PR opened at the spec commit: {Url}", url);
        return url;
    }

    private async Task RecordAsync(
        PipelineContext pipeline, string repoName, string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        pipeline.Set(ContextKeys.SpecPullRequestUrl, url!);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        // p0347 lists it: the run surfaces the spec PR the moment it exists, so a
        // parked run is not a run that silently produced nothing.
        await events.PublishAsync(
            new PullRequestOutcomeEvent(runId!, repoName, "opened", DateTimeOffset.UtcNow, url, null), ct);
    }
}
