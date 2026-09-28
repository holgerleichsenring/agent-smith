using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Services.Webhooks;

namespace AgentSmith.Server.Services.ChatLaunch;

/// <summary>
/// The run context for a pull request a chat command names by number only. The host is asked
/// for the pull request's head, base, branch and author through the repo's diff provider, and
/// the context is built by the same factory a pull-request webhook uses, so a chat-requested
/// scan checks out and scans the same code a label-requested scan does.
/// </summary>
public sealed class ChatPrContextResolver(
    IPrDiffProviderFactory diffProviders,
    PrRunContextFactory contextFactory)
{
    /// <summary>The one repo of the project that has pull requests, or null when the project
    /// has none or several — a bare number cannot say which repo it belongs to.</summary>
    public RepoConnection? PullRequestRepo(ResolvedProject project)
    {
        var candidates = project.Repos
            .Where(r => r.Type is RepoType.GitHub or RepoType.GitLab or RepoType.AzureDevOps)
            .ToList();
        return candidates.Count == 1 ? candidates[0] : null;
    }

    public async Task<Dictionary<string, object>> ResolveAsync(
        RepoConnection repo, string prNumber, CancellationToken ct)
    {
        var diff = await diffProviders.Create(repo).GetDiffAsync(prNumber, ct);
        return contextFactory.FromDiff(prNumber, diff, repo.Name);
    }
}
