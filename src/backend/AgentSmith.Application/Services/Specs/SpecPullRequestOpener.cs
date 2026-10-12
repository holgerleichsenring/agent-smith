using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: find-or-create at the spec commit. Always a DRAFT — the run is still
/// working and no phase has been verified yet; CommitAndPR finds this pull request,
/// refreshes its body with the phase-status table and promotes it to ready ONLY when
/// the whole sequence is through. Failure is logged and swallowed: the specs are on
/// the branch either way, and the run must not die because a PR could not be opened.
/// 2026-10-12-f5f7e: one per series repository, over that repository's sub-series. A repository
/// whose sub-series holds no spec only finds one — as does a repository the scope read that the set
/// no longer names, so the ticket comment can list its pull request as dropped.
/// </summary>
public sealed class SpecPullRequestOpener(
    ISourceProviderFactory sourceFactory,
    IEventPublisher events,
    ILogger<SpecPullRequestOpener> logger) : ISpecPullRequestOpener
{
    public async Task<IReadOnlyDictionary<string, string>> OpenAsync(
        PipelineContext pipeline, SeriesPlacement placement, SeriesScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(scope);
        if (!pipeline.TryGet<Repository>(ContextKeys.Repository, out var repository) || repository is null)
            return SpecPullRequests.Of(pipeline);
        foreach (var member in placement.Members)
        {
            var own = placement.Set.OfRepository(member.Name ?? string.Empty);
            var url = await OpenOneAsync(pipeline, repository, member, own, own.Phases.Count > 0, cancellationToken);
            await RecordAsync(pipeline, member.Name, url, cancellationToken);
        }
        // A dropped repository's pull request is only listed — it is no outcome of this run.
        foreach (var dropped in Dropped(placement, scope))
            if (await OpenOneAsync(pipeline, repository, dropped, placement.Set, false, cancellationToken) is { } url)
                SpecPullRequests.Record(pipeline, dropped.Name, url);
        return SpecPullRequests.Of(pipeline);
    }

    // A repository the run read off the branch that the placed set no longer names.
    private static IEnumerable<RepoConnection> Dropped(SeriesPlacement placement, SeriesScope scope) =>
        scope.MembersRead
            .Where(name => !placement.Names.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Select(name => scope.RunRepos.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            .OfType<RepoConnection>();

    private async Task<string?> OpenOneAsync(
        PipelineContext pipeline, Repository repository, RepoConnection repo, SpecSet set, bool createIfMissing,
        CancellationToken cancellationToken)
    {
        var branchRepo = new Repository(repository.CurrentBranch, repo.Url ?? string.Empty);
        try
        {
            var provider = sourceFactory.Create(repo);
            // 2026-09-13-a284: THIS is the pull request an epic child actually gets — opened
            // here, at the spec commit, long before CommitAndPR. Until now it was opened
            // against the provider's default branch and never moved, so a slice's review
            // showed its predecessors' work as well as its own.
            var target = Handlers.PullRequestTargets.For(pipeline, repo.Name);
            return await provider.FindOpenPullRequestAsync(branchRepo, cancellationToken)
                ?? (createIfMissing ? await CreateAsync(provider, branchRepo, pipeline, set, target, cancellationToken) : null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Repo}: could not open the draft PR at the spec commit", repo.Name);
            return null;
        }
    }

    private async Task<string> CreateAsync(
        ISourceProvider provider, Repository branchRepo, PipelineContext pipeline,
        SpecSet set, BranchName? target, CancellationToken ct)
    {
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        var url = await provider.CreatePullRequestAsync(
            branchRepo, ticket?.Title ?? set.Key,
            SpecPrBody.BuildInitial(set), ct, linkedTicketId: ticket?.Id, isDraft: true,
            targetBranch: target);
        logger.LogInformation("Draft PR opened at the spec commit: {Url}", url);
        return url;
    }

    private async Task RecordAsync(
        PipelineContext pipeline, string repoName, string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        SpecPullRequests.Record(pipeline, repoName, url!);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        // p0347 lists it: the run surfaces the spec PR the moment it exists, so a
        // parked run is not a run that silently produced nothing.
        await events.PublishAsync(
            new PullRequestOutcomeEvent(runId!, repoName, "opened", DateTimeOffset.UtcNow, url, null), ct);
    }
}
