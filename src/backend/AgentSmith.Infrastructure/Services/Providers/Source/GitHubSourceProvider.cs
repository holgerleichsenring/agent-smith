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
        TicketId? linkedTicketId = null)
    {
        var client = CreateGitHubClient();
        var targetBranch = await GetDefaultBranchAsync(client);

        // GitHub auto-links + auto-closes when the PR body references the issue
        // via "Closes #N" / "Fixes #N" syntax. Appended as a footer so the
        // operator-facing body content stays at the top.
        var body = linkedTicketId is null
            ? description
            : $"{description}\n\nCloses #{linkedTicketId.Value}";

        var pr = await client.PullRequest.Create(
            _owner, _repo,
            new NewPullRequest(title, repository.CurrentBranch.Value, targetBranch)
            {
                Body = body
            });

        _logger.LogInformation("Pull request created: {Url}", pr.HtmlUrl);
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
