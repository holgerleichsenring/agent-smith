using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// Posts reply comments on GitHub PRs via the Octokit API.
/// Uses the issue comment endpoint (GitHub treats PR comments as issue comments).
/// </summary>
public sealed class GitHubPrCommentReplyService(
    SecretsProvider secrets,
    ILogger<GitHubPrCommentReplyService> logger) : IPrCommentReplyService
{
    public async Task ReplyAsync(CommentIntent originalComment, string text, CancellationToken cancellationToken)
    {
        var (owner, repo) = ParseRepoFullName(originalComment.RepoFullName);
        var prNumber = int.Parse(originalComment.PrIdentifier);

        var client = CreateGitHubClient();
        await client.Issue.Comment.Create(owner, repo, prNumber, text);

        logger.LogInformation(
            "Posted reply on {Repo}#{Pr} (original comment {CommentId})",
            originalComment.RepoFullName, prNumber, originalComment.CommentId);
    }

    private GitHubClient CreateGitHubClient()
    {
        var token = secrets.GetRequired("GITHUB_TOKEN");
        var client = new GitHubClient(new ProductHeaderValue("AgentSmith"));
        client.Credentials = new Credentials(token);
        return client;
    }

    private static (string Owner, string Repo) ParseRepoFullName(string fullName)
    {
        var parts = fullName.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid repository full name: '{fullName}'. Expected 'owner/repo'.");

        return (parts[0], parts[1]);
    }
}
