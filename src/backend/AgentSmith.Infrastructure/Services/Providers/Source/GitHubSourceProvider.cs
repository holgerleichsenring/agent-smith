using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Octokit;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Source provider for GitHub repositories. CheckoutAsync is metadata-only —
/// the actual git clone happens sandbox-side via Step{Kind=Run, Command=git, ...}.
/// Default-branch resolution stays here (it is metadata, not git plumbing).
/// </summary>
public sealed class GitHubSourceProvider : ISourceProvider, IPrCommentProvider, IPrReviewThreadReader, IPrReviewActReader, IPrReviewStateReader, IChangedPullRequestLister, IOpenPullRequestSource, IPullRequestHeadReader, IPullRequestCloser
{
    private readonly GitHubRepoAddress _address;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _cloneUrl;
    private readonly string _token;
    private readonly IGitHubClientFactory _clientFactory;
    private readonly ILogger<GitHubSourceProvider> _logger;
    private readonly GitHubPullRequestUpdater _pullRequests;
    private readonly GitHubPullRequestMerger _merger;
    // 2026-09-13-a284: where an open pull request POINTS, separate from what it says.
    private readonly GitHubPullRequestTarget _prTarget;
    // p0500: the repository's own default branch wins; connection.DefaultBranch is the fallback.
    private readonly DefaultBranchResolver _defaultBranch;
    // 2026-09-22-b6ad: putting files on a branch without a checkout, through this same client.
    private readonly GitHubBranchWrite _branchWrite;
    // 2026-10-12-6bfaa: reading a named branch, the default-branch reads included.
    private readonly GitHubBranchRead _branchRead;

    public string ProviderType => "GitHub";

    public GitHubSourceProvider(
        GitHubSourceConnection connection,
        IGitHubClientFactory clientFactory,
        ILogger<GitHubSourceProvider> logger)
    {
        _address = GitHubRepoAddress.Parse(connection.RepoUrl);
        (_owner, _repo) = (_address.Owner, _address.Repo);
        _cloneUrl = connection.RepoUrl.EndsWith(".git") ? connection.RepoUrl : $"{connection.RepoUrl}.git";
        _token = connection.Token;
        _clientFactory = clientFactory;
        _logger = logger;
        _pullRequests = new GitHubPullRequestUpdater(_address, clientFactory, _token, logger);
        _merger = new GitHubPullRequestMerger(_address, clientFactory, _token, logger);
        _prTarget = new GitHubPullRequestTarget(_address, clientFactory, _token, logger);
        _defaultBranch = new DefaultBranchResolver(
            connection.DefaultBranch, $"{_owner}/{_repo}", logger);
        _branchWrite = new GitHubBranchWrite(_address, clientFactory, _token, logger);
        _branchRead = new GitHubBranchRead(_address, clientFactory, _token);
    }

    public async Task<BranchWriteResult> WriteFilesToBranchAsync(
        BranchName branch, IReadOnlyList<RepoFile> files, string message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        return await _branchWrite.WriteAsync(
            branch.Value, await GetDefaultBranchAsync(CreateGitHubClient()), files, message,
            cancellationToken);
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
        TicketId? linkedTicketId = null, bool isDraft = false, BranchName? targetBranch = null)
    {
        var client = CreateGitHubClient();
        // 2026-09-13-a284: the base the caller resolved WINS; a null target falls through
        // to the default-branch resolver, which honours the configured override.
        var target = targetBranch?.Value ?? await GetDefaultBranchAsync(client);

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
                new NewPullRequest(title, repository.CurrentBranch.Value, target)
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

    // p0390: the same search the already-exists catch above uses, exposed so
    // CommitAndPR can reuse the draft PR the work-spec commit already opened
    // instead of creating a second one on the same branch.
    public async Task<string?> FindOpenPullRequestAsync(
        Repository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _ = cancellationToken;
        try
        {
            var existing = await CreateGitHubClient().PullRequest.GetAllForRepository(
                _owner, _repo,
                new PullRequestRequest
                {
                    Head = $"{_owner}:{repository.CurrentBranch.Value}",
                    State = ItemStateFilter.Open,
                });
            return existing.FirstOrDefault()?.HtmlUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PR lookup for branch {Branch} failed", repository.CurrentBranch);
            return null;
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

    private Task<string> GetDefaultBranchAsync(IGitHubClient client) =>
        _defaultBranch.ResolveAsync(async _ => (await client.Repository.Get(_owner, _repo)).DefaultBranch,
            CancellationToken.None);

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

    // 2026-10-08-f147: only the token's own marked comments go — a marker is text anyone can paste.
    public async Task<int> DeleteCommentsByMarkerAsync(
        string prIdentifier, string markerPrefix, CancellationToken cancellationToken = default)
    {
        var prNumber = int.Parse(prIdentifier);
        var client = CreateGitHubClient();
        var self = (await client.User.Current()).Login;
        bool Ours(string? body, User? user) =>
            string.Equals(user?.Login, self, StringComparison.OrdinalIgnoreCase)
            && body?.StartsWith(markerPrefix, StringComparison.Ordinal) == true;
        var deleted = 0;
        foreach (var comment in await client.PullRequest.ReviewComment.GetAll(_owner, _repo, prNumber))
            if (Ours(comment.Body, comment.User))
            {
                await client.PullRequest.ReviewComment.Delete(_owner, _repo, comment.Id);
                deleted++;
            }
        deleted += await DeleteMarkedIssueCommentsAsync(client, prNumber, Ours);
        _logger.LogInformation("Deleted {Count} marked comment(s) on PR #{PrNumber}", deleted, prNumber);
        return deleted;
    }

    private async Task<int> DeleteMarkedIssueCommentsAsync(
        IGitHubClient client, int prNumber, Func<string?, User?, bool> ours)
    {
        var deleted = 0;
        foreach (var comment in await client.Issue.Comment.GetAllForIssue(_owner, _repo, prNumber))
            if (ours(comment.Body, comment.User))
            {
                await client.Issue.Comment.Delete(_owner, _repo, comment.Id);
                deleted++;
            }
        return deleted;
    }

    public Task<bool> UpdatePullRequestBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken) =>
        _pullRequests.UpdateBodyAsync(prUrl, newBody, cancellationToken);

    public Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken) =>
        _pullRequests.MarkReadyAsync(prUrl, cancellationToken);

    public Task<string?> ReadPullRequestBaseAsync(string prUrl, CancellationToken cancellationToken) =>
        _prTarget.ReadBaseAsync(prUrl, cancellationToken);

    public Task<bool> RetargetPullRequestAsync(
        string prUrl, BranchName target, CancellationToken cancellationToken) =>
        _prTarget.MoveAsync(prUrl, target.Value, cancellationToken);

    // p0490: the branch is what a LOCAL repository needs to finish a "pull request";
    // GitHub recovers everything it needs from the URL.
    public Task<PullRequestCompletion> CompletePullRequestAsync(
        string prUrl, BranchName sourceBranch, CancellationToken cancellationToken) =>
        _merger.CompleteAsync(prUrl, cancellationToken);

    public async Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken) =>
        await _branchRead.TryReadFileAsync(path, await GetDefaultBranchAsync(CreateGitHubClient()), cancellationToken);

    public async Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken) =>
        await _branchRead.ListDirectoryAsync(path, await GetDefaultBranchAsync(CreateGitHubClient()), cancellationToken);

    public Task<bool> BranchExistsAsync(BranchName branch, CancellationToken cancellationToken) =>
        _branchRead.ExistsAsync(branch.Value, cancellationToken);

    public Task<string?> TryReadFileAsync(string path, BranchName branch, CancellationToken cancellationToken) =>
        _branchRead.TryReadFileAsync(path, branch.Value, cancellationToken);

    public Task<IReadOnlyList<string>> ListDirectoryAsync(
        string path, BranchName branch, CancellationToken cancellationToken) =>
        _branchRead.ListDirectoryAsync(path, branch.Value, cancellationToken);

    private IGitHubClient CreateGitHubClient() => _clientFactory.Create(_address, _token);

    // 2026-10-08-e8b9d: the review is read by a collaborator over this provider's own connection.
    public Task<IReadOnlyList<PrReviewThread>> ListAsync(string prUrl, CancellationToken cancellationToken) =>
        new GitHubPrReviewThreadReader(CreateGitHubClient(), _address, _logger).ListAsync(prUrl, cancellationToken);

    public Task<IReadOnlyList<PrReviewNote>> ChangesRequestedAsync(string prUrl, CancellationToken cancellationToken) =>
        new GitHubPrReviewActReader(CreateGitHubClient(), _address).ChangesRequestedAsync(prUrl, cancellationToken);

    public Task<PrReviewState> ReadAsync(string prUrl, CancellationToken cancellationToken) =>
        new GitHubPrReviewStateReader(CreateGitHubClient(), _address).ReadAsync(prUrl, cancellationToken);

    public Task CloseAsync(string prUrl, CancellationToken cancellationToken) => new GitHubPullRequestClose(_owner, _repo, CreateGitHubClient()).CloseAsync(prUrl, cancellationToken);

    public async Task<PullRequestHead> HeadAsync(string prNumber, CancellationToken cancellationToken)
    {
        var pr = await CreateGitHubClient().PullRequest.Get(_owner, _repo, int.Parse(prNumber));
        return new PullRequestHead(pr.Head?.Ref,
            string.Equals(pr.Head?.Repository?.FullName, $"{_owner}/{_repo}", StringComparison.OrdinalIgnoreCase), pr.HtmlUrl);
    }

    // 2026-10-08-9e6e / 10b0: the open pull requests that may carry a rework act, and every open one with its comments.
    public IOpenPullRequestLister OpenPullRequests() => new GitHubOpenPullRequests(CreateGitHubClient(), _owner, _repo, _address.RepoUrl);
    public Task<ChangedPage> ChangedSinceAsync(DateTimeOffset since, string? resume, int maxPages, CancellationToken ct) => new GitHubChangedPullRequests(CreateGitHubClient(), _address).ChangedSinceAsync(since, resume, maxPages, ct);
}
