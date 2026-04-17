using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Azure DevOps source provider. Delegates git plumbing to <see cref="AzureGitOperations"/>.
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
    private readonly AzureGitOperations _git = new(personalAccessToken, logger);

    public string ProviderType => "AzureRepos";

    public Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken)
    {
        var localPath = GetLocalPath();
        _git.EnsureCloned(_cloneUrl, localPath);

        var target = branch ?? new BranchName("main");

        using var repo = new LibGit2Sharp.Repository(localPath);
        _git.CheckoutBranch(repo, target.Value);
        logger.LogInformation("Checked out branch {Branch} in {Path}", target, localPath);
        return Task.FromResult(new Repository(localPath, target, _cloneUrl));
    }

    public Task CommitAndPushAsync(Repository repository, string message, CancellationToken cancellationToken)
    {
        using var repo = new LibGit2Sharp.Repository(repository.LocalPath);
        _git.StageAllChanges(repo);
        _git.CommitChanges(repo, message);
        _git.PushToRemote(repo);
        logger.LogInformation("Committed and pushed changes: {Message}", message);
        return Task.CompletedTask;
    }

    public async Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken)
    {
        var client = CreateGitClient();
        var src = $"refs/heads/{repository.CurrentBranch.Value}";
        const string tgt = "refs/heads/main";
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

    private string GetLocalPath() =>
        Path.Combine(Path.GetTempPath(), "agentsmith", "azuredevops", project, repoName);

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
