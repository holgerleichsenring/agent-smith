using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-25-9992e: a refresh the series master asks for — of a ticket's pull request or of a series' final one: a
/// refresh-base run through <see cref="BaseRefreshStart"/>, the one start every refresh takes, unless one is at work on that
/// pull request already. No conflict-due rule: the master judged it due. Answers the sentence of what happened.
/// </summary>
public sealed class BaseRefreshAsked(IServiceScopeFactory scopes, BaseRefreshStart start, ISourceProviderFactory sources)
{
    public async Task<string> StartAsync(ResolvedProject project, RepoConnection repo, RefreshedPullRequest pullRequest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pullRequest);
        using var scope = scopes.CreateScope();
        var tried = (await scope.ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>().NewestRefreshesAsync([pullRequest.Url], ct))
            .GetValueOrDefault(pullRequest.Url);
        if (tried is { InFlight: true }) return $"{pullRequest.Url}: a refresh is already at work on it (run {tried.RunId}).";
        var started = await start.StartAsync(scope.ServiceProvider, project, repo, sources.Create(repo), pullRequest, ct);
        return started.RunId is { } runId
            ? $"{pullRequest.Url}: a refresh-base run brings it up to date with its base (run {runId})."
            : $"{pullRequest.Url}: not refreshed — {started.Refusal}";
    }
}
