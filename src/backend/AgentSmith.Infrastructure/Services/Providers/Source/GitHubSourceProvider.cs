using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source provider for GitHub repositories. CheckoutAsync is metadata-only —
/// the actual git clone happens sandbox-side via Step{Kind=Run, Command=git, ...}.
/// Default-branch resolution stays here (it is metadata, not git plumbing).
/// </summary>
public sealed class GitHubSourceProvider : ISourceProvider, IPrCommentProvider
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _cloneUrl;
    private readonly string _token;
    private readonly string? _configuredDefaultBranch;
    private readonly IGitHubClientFactory _clientFactory;
    private readonly ILogger<GitHubSourceProvider> _logger;
    private string? _cachedDefaultBranch;

    public string ProviderType => "GitHub";

    public GitHubSourceProvider(
        GitHubSourceConnection connection,
        IGitHubClientFactory clientFactory,
        ILogger<GitHubSourceProvider> logger)
    {
        (_owner, _repo) = ParseGitHubUrl(connection.RepoUrl);
        _cloneUrl = connection.RepoUrl.EndsWith(".git") ? connection.RepoUrl : $"{connection.RepoUrl}.git";
        _token = connection.Token;
        _clientFactory = clientFactory;
        _configuredDefaultBranch = connection.DefaultBranch;
        _logger = logger;
    }

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await CreateGitHubClient().Repository.Get(_owner, _repo);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GitHub source probe failed for {Owner}/{Repo}", _owner, _repo);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<Repository> CheckoutAsync(
        BranchName? branch, CancellationToken cancellationToken)
    {
        var target = branch ?? new BranchName(await GetDefaultBranchAsync(CreateGitHubClient()));
        _logger.LogInformation(
            "Resolved metadata for {Url} on branch {Branch}", _cloneUrl, target);
        return new Repository(target, _cloneUrl);
    }

    public async Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken,
        TicketId? linkedTicketId = null, bool isDraft = false)
    {
        var client = CreateGitHubClient();
        var targetBranch = await GetDefaultBranchAsync(client);

        // GitHub auto-links + auto-closes when the PR body references the issue
        // via "Closes #N" / "Fixes #N" syntax. Appended as a footer so the
        // operator-facing body content stays at the top.
        var body = linkedTicketId is null
            ? description
            : $"{description}\n\nCloses #{linkedTicketId.Value}";

        try
        {
            var pr = await client.PullRequest.Create(
                _owner, _repo,
                new NewPullRequest(title, repository.CurrentBranch.Value, targetBranch)
                {
                    Body = body,
                    Draft = isDraft
                });

            _logger.LogInformation("Pull request created: {Url}", pr.HtmlUrl);
            return pr.HtmlUrl;
        }
        catch (ApiValidationException ex) when (PullRequestAlreadyExists(ex))
        {
            // p0298: a re-run's branch already has an open PR — GitHub 422s. Reuse it
            // so the ticket doesn't fail at the PR step.
            return await FindOpenPullRequestUrlAsync(client, repository.CurrentBranch.Value);
        }
    }

    internal static bool PullRequestAlreadyExists(ApiValidationException ex) =>
        ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
        || (ex.ApiError?.Errors?.Any(
            e => e.Message?.Contains("already exists", StringComparison.OrdinalIgnoreCase) == true) ?? false);

    private async Task<string> FindOpenPullRequestUrlAsync(IGitHubClient client, string sourceBranch)
    {
        var existing = await client.PullRequest.GetAllForRepository(
            _owner, _repo,
            new PullRequestRequest { Head = $"{_owner}:{sourceBranch}", State = ItemStateFilter.Open });
        var pr = existing.FirstOrDefault()
            ?? throw new ProviderException(
                ProviderType, $"PR already exists but none found open for source branch '{sourceBranch}'.");

        _logger.LogInformation("Reusing existing pull request: {Url}", pr.HtmlUrl);
        return pr.HtmlUrl;
    }

    private async Task<string> GetDefaultBranchAsync(IGitHubClient client)
    {
        if (_configuredDefaultBranch is not null)
            return _configuredDefaultBranch;

        if (_cachedDefaultBranch is not null)
            return _cachedDefaultBranch;

        try
        {
            var repo = await client.Repository.Get(_owner, _repo);
            _cachedDefaultBranch = repo.DefaultBranch;
            _logger.LogDebug("Resolved default branch from GitHub API: {Branch}", _cachedDefaultBranch);
            return _cachedDefaultBranch;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve default branch from GitHub API, falling back to 'main'");
            _cachedDefaultBranch = "main";
            return _cachedDefaultBranch;
        }
    }

    public async Task PostCommentAsync(
        string prIdentifier, string markdown, CancellationToken cancellationToken = default)
    {
        var prNumber = int.Parse(prIdentifier);
        var client = CreateGitHubClient();
        await client.Issue.Comment.Create(_owner, _repo, prNumber, markdown);
        _logger.LogInformation("Posted comment on PR #{PrNumber}", prNumber);
    }

    // p0167c: one review-create call carries the whole inline batch. Octokit's
    // typed PullRequestReviewCreate only speaks diff positions, so the raw
    // endpoint is used with the modern line/side anchors (new-side numbering —
    // the PrReviewInlineComment convention). The summary is a separate issue
    // comment (not the review body) so the marker-overwrite pass can delete it.
    public async Task PostReviewBatchAsync(
        string prIdentifier, PrReviewSummary review, CancellationToken cancellationToken = default)
    {
        var prNumber = int.Parse(prIdentifier);
        var client = CreateGitHubClient();
        if (review.InlineComments.Count > 0)
        {
            var payload = new Dictionary<string, object>
            {
                ["event"] = "COMMENT",
                ["comments"] = review.InlineComments.Select(ToReviewCommentPayload).ToList(),
            };
            await client.Connection.Post(
                new Uri($"repos/{_owner}/{_repo}/pulls/{prNumber}/reviews", UriKind.Relative),
                payload, "application/vnd.github+json", cancellationToken);
            _logger.LogInformation(
                "Posted review with {Count} inline comment(s) on PR #{PrNumber}",
                review.InlineComments.Count, prNumber);
        }
        await client.Issue.Comment.Create(_owner, _repo, prNumber, review.TopLevelComment);
    }

    private static Dictionary<string, object> ToReviewCommentPayload(PrReviewInlineComment comment)
    {
        var payload = new Dictionary<string, object>
        {
            ["path"] = comment.File,
            ["line"] = comment.EndLine,
            ["side"] = "RIGHT",
            ["body"] = comment.Body,
        };
        if (comment.StartLine < comment.EndLine)
        {
            payload["start_line"] = comment.StartLine;
            payload["start_side"] = "RIGHT";
        }
        return payload;
    }

    public async Task<int> DeleteCommentsByMarkerAsync(
        string prIdentifier, string markerPrefix, CancellationToken cancellationToken = default)
    {
        var prNumber = int.Parse(prIdentifier);
        var client = CreateGitHubClient();
        var deleted = 0;
        foreach (var comment in await client.PullRequest.ReviewComment.GetAll(_owner, _repo, prNumber))
            if (comment.Body?.StartsWith(markerPrefix, StringComparison.Ordinal) == true)
            {
                await client.PullRequest.ReviewComment.Delete(_owner, _repo, comment.Id);
                deleted++;
            }
        deleted += await DeleteMarkedIssueCommentsAsync(client, prNumber, markerPrefix);
        _logger.LogInformation("Deleted {Count} marked comment(s) on PR #{PrNumber}", deleted, prNumber);
        return deleted;
    }

    private async Task<int> DeleteMarkedIssueCommentsAsync(
        IGitHubClient client, int prNumber, string markerPrefix)
    {
        var deleted = 0;
        foreach (var comment in await client.Issue.Comment.GetAllForIssue(_owner, _repo, prNumber))
            if (comment.Body?.StartsWith(markerPrefix, StringComparison.Ordinal) == true)
            {
                await client.Issue.Comment.Delete(_owner, _repo, comment.Id);
                deleted++;
            }
        return deleted;
    }

    public async Task<bool> UpdatePullRequestBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken)
    {
        if (!TryParsePullNumber(prUrl, out var prNumber)) return false;
        try
        {
            var client = CreateGitHubClient();
            await client.PullRequest.Update(_owner, _repo, prNumber,
                new PullRequestUpdate { Body = newBody });
            _logger.LogInformation("Updated PR body for #{Pr}", prNumber);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update PR body for #{Pr}", prNumber);
            return false;
        }
    }

    private static bool TryParsePullNumber(string prUrl, out int prNumber)
    {
        prNumber = 0;
        var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"/pull/(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out prNumber);
    }

    public async Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        var client = CreateGitHubClient();
        var branch = await GetDefaultBranchAsync(client);
        try
        {
            var contents = await client.Repository.Content.GetAllContentsByRef(
                _owner, _repo, path, branch);
            // GetAllContentsByRef on a file returns a single-element list with
            // the file's text. The 'Content' field is null when the file is a
            // directory; for our use case (.agentsmith/context.yaml) it is a
            // regular file.
            return contents.Count == 0 ? null : contents[0].Content;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        var client = CreateGitHubClient();
        var branch = await GetDefaultBranchAsync(client);
        try
        {
            // GetAllContentsByRef on a directory returns one entry per child;
            // each has Name. For a file path the list is one element with that
            // file's name — caller can filter by sub-dir if needed.
            var contents = await client.Repository.Content.GetAllContentsByRef(
                _owner, _repo, path, branch);
            return contents.Select(c => c.Name).ToList();
        }
        catch (NotFoundException)
        {
            return [];
        }
    }

    private IGitHubClient CreateGitHubClient() => _clientFactory.Create(_token);

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var uri = new Uri(url.Replace(".git", ""));
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2)
            throw new ConfigurationException($"Invalid GitHub URL: {url}");

        return (segments[0], segments[1]);
    }
}
