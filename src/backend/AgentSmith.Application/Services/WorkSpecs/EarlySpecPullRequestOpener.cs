using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: find-or-create at the spec commit. Always a DRAFT — the run is still
/// working, and the keystone's ready/draft verdict is not known yet; CommitAndPR
/// finds this PR and updates its body with the outcome instead of opening a
/// second one. Failure is logged and swallowed: the spec is on the branch either
/// way, and the run must not die because a PR could not be opened early.
/// </summary>
public sealed class EarlySpecPullRequestOpener(
    ISourceProviderFactory sourceFactory,
    IEventPublisher events,
    ILogger<EarlySpecPullRequestOpener> logger) : IEarlySpecPullRequestOpener
{
    public async Task<string?> OpenAsync(
        PipelineContext pipeline, RepoConnection carryingRepo, WorkSpecArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(carryingRepo);
        ArgumentNullException.ThrowIfNull(artifact);
        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) || repository is null)
            return null;
        var branchRepo = new Repository(repository.CurrentBranch, carryingRepo.Url ?? string.Empty);
        try
        {
            var provider = sourceFactory.Create(carryingRepo);
            var url = await provider.FindOpenPullRequestAsync(branchRepo, cancellationToken)
                ?? await CreateAsync(provider, branchRepo, pipeline, artifact, cancellationToken);
            await RecordAsync(pipeline, carryingRepo.Name, url, cancellationToken);
            return url;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "{Repo}: could not open the draft PR at the work-spec commit", carryingRepo.Name);
            return null;
        }
    }

    private async Task<string> CreateAsync(
        ISourceProvider provider, Repository branchRepo, PipelineContext pipeline,
        WorkSpecArtifact artifact, CancellationToken ct)
    {
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        var url = await provider.CreatePullRequestAsync(
            branchRepo, ticket?.Title ?? artifact.Spec.Goal,
            WorkSpecPrBody.Build(artifact), ct, linkedTicketId: ticket?.Id, isDraft: true);
        logger.LogInformation("Draft PR opened at the work-spec commit: {Url}", url);
        return url;
    }

    private async Task RecordAsync(
        PipelineContext pipeline, string repoName, string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        pipeline.Set(ContextKeys.WorkSpecPullRequestUrl, url!);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        // p0347 lists it: the run surfaces the spec PR the moment it exists, so a
        // parked run is not a run that silently produced nothing.
        await events.PublishAsync(
            new PullRequestOutcomeEvent(runId!, repoName, "opened", DateTimeOffset.UtcNow, url, null), ct);
    }
}
