using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Azure DevOps source provider. CheckoutAsync is metadata-only — the git clone
/// happens sandbox-side via Step{Kind=Run, Command=git, ...}. Default-branch
/// resolution via DevOps REST stays (it is metadata, not git plumbing).
/// </summary>
public sealed class AzureReposSourceProvider(
    string organizationUrl,
    string project,
    string repoName,
    string personalAccessToken,
    ILogger<AzureReposSourceProvider> logger) : ISourceProvider, IPrCommentProvider
{
    private readonly string _organizationUrl = organizationUrl.TrimEnd('/');
    private readonly string _cloneUrl = $"{organizationUrl.TrimEnd('/')}/{project}/_git/{repoName}";
    private string? _cachedDefaultBranch;

    public string ProviderType => "AzureRepos";

    public async Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken)
    {
        var target = branch ?? new BranchName(await GetDefaultBranchAsync(cancellationToken));
        logger.LogInformation(
            "Resolved metadata for {Url} on branch {Branch}", _cloneUrl, target);
        return new Repository(target, _cloneUrl);
    }

    public async Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken)
    {
        var client = CreateGitClient();
        var src = $"refs/heads/{repository.CurrentBranch.Value}";
        var tgt = $"refs/heads/{await GetDefaultBranchAsync(cancellationToken)}";
        var pr = new GitPullRequest
        {
            Title = title, Description = description,
            SourceRefName = src, TargetRefName = tgt
        };

        try
        {
            var created = await client.CreatePullRequestAsync(
                pr, project, repoName, cancellationToken: cancellationToken);
            logger.LogInformation("Pull request created: {Url}", BuildPrUrl(created.PullRequestId));
            return BuildPrUrl(created.PullRequestId);
        }
        catch (Exception ex) when (ex.Message.Contains("TF401179") || ex.Message.Contains("already exists"))
        {
            return await FindExistingPrUrlAsync(client, src, tgt, cancellationToken);
        }
    }

    public async Task PostCommentAsync(
        string prIdentifier, string markdown, CancellationToken cancellationToken = default)
    {
        var prId = int.Parse(prIdentifier);
        var gitClient = await CreateConnectionAsync(cancellationToken);
        var thread = new GitPullRequestCommentThread
        {
            Comments = [new Comment { Content = markdown, CommentType = CommentType.Text }],
            Status = CommentThreadStatus.Active
        };
        await gitClient.CreateThreadAsync(thread, project, repoName, prId, cancellationToken: cancellationToken);
        logger.LogInformation("Posted comment on PR #{PrId}", prId);
    }

    private async Task<string> GetDefaultBranchAsync(CancellationToken cancellationToken)
    {
        if (_cachedDefaultBranch is not null)
            return _cachedDefaultBranch;

        try
        {
            var client = CreateGitClient();
            var repo = await client.GetRepositoryAsync(
                project, repoName, cancellationToken: cancellationToken);
            var raw = repo.DefaultBranch ?? "refs/heads/main";
            _cachedDefaultBranch = raw.StartsWith("refs/heads/", StringComparison.Ordinal)
                ? raw["refs/heads/".Length..] : raw;
            logger.LogDebug("Resolved default branch from Azure DevOps API: {Branch}", _cachedDefaultBranch);
            return _cachedDefaultBranch;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve default branch from Azure DevOps API, falling back to 'main'");
            _cachedDefaultBranch = "main";
            return _cachedDefaultBranch;
        }
    }

    private async Task<string> FindExistingPrUrlAsync(
        GitHttpClient client, string src, string tgt, CancellationToken cancellationToken)
    {
        var criteria = new GitPullRequestSearchCriteria
            { SourceRefName = src, TargetRefName = tgt, Status = PullRequestStatus.Active };
        var existing = (await client.GetPullRequestsAsync(
            project, repoName, criteria, cancellationToken: cancellationToken)).FirstOrDefault()
            ?? throw new ProviderException(ProviderType, "PR already exists but could not be found.");
        logger.LogInformation("Found existing pull request: {Url}", BuildPrUrl(existing.PullRequestId));
        return BuildPrUrl(existing.PullRequestId);
    }

    private string BuildPrUrl(int prId) =>
        $"{_organizationUrl}/{project}/_git/{repoName}/pullrequest/{prId}";

    private GitHttpClient CreateGitClient()
    {
        var creds = new VssBasicCredential(string.Empty, personalAccessToken);
        return new VssConnection(new Uri(_organizationUrl), creds).GetClient<GitHttpClient>();
    }

    private async Task<GitHttpClient> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var creds = new VssBasicCredential(string.Empty, personalAccessToken);
        return await new VssConnection(new Uri(_organizationUrl), creds)
            .GetClientAsync<GitHttpClient>(cancellationToken);
    }
}
