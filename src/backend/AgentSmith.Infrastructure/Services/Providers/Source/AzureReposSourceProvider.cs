using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Repository = AgentSmith.Domain.Entities.Repository;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Azure DevOps source provider. CheckoutAsync is metadata-only — the git clone
/// happens sandbox-side via Step{Kind=Run, Command=git, ...}. Default-branch
/// resolution via DevOps REST stays (it is metadata, not git plumbing).
/// </summary>
public sealed class AzureReposSourceProvider(
    AzureReposSourceConnection connection,
    IAzDoClientFactory clientFactory,
    ILogger<AzureReposSourceProvider> logger) : ISourceProvider, IPrCommentProvider
{
    private readonly string _organizationUrl = connection.OrganizationUrl.TrimEnd('/');
    private readonly string _project = connection.Project;
    private readonly string _repoName = connection.RepoName;
    private readonly string _personalAccessToken = connection.PersonalAccessToken;
    private readonly string _cloneUrl = $"{connection.OrganizationUrl.TrimEnd('/')}/{connection.Project}/_git/{connection.RepoName}";
    private readonly string? _configuredDefaultBranch = connection.DefaultBranch;
    private string? _cachedDefaultBranch;

    public string ProviderType => "AzureRepos";

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = await CreateConnectionAsync(cancellationToken);
            await client.GetRepositoryAsync(_project, _repoName, cancellationToken: cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Azure Repos source probe failed for {Project}/{Repo}", _project, _repoName);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken)
    {
        var target = branch ?? new BranchName(await GetDefaultBranchAsync(cancellationToken));
        logger.LogInformation(
            "Resolved metadata for {Url} on branch {Branch}", _cloneUrl, target);
        return new Repository(target, _cloneUrl);
    }

    public async Task<string> CreatePullRequestAsync(
        Repository repository, string title, string description,
        CancellationToken cancellationToken,
        TicketId? linkedTicketId = null)
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
                pr, _project, _repoName, cancellationToken: cancellationToken);
            logger.LogInformation("Pull request created: {Url}", BuildPrUrl(created.PullRequestId));

            // Native AzDO work-item-to-PR link via WorkItem relations. Done after
            // create so a relation failure does not block PR creation — operators
            // can manually link later, the PR is the load-bearing artifact.
            if (linkedTicketId is not null
                && int.TryParse(linkedTicketId.Value, out var workItemId))
            {
                await LinkWorkItemToPrAsync(workItemId, created.PullRequestId, cancellationToken);
            }

            return BuildPrUrl(created.PullRequestId);
        }
        catch (Exception ex) when (ex.Message.Contains("TF401179") || ex.Message.Contains("already exists"))
        {
            return await FindExistingPrUrlAsync(client, src, tgt, cancellationToken);
        }
    }

    private async Task LinkWorkItemToPrAsync(int workItemId, int pullRequestId, CancellationToken ct)
    {
        try
        {
            // AzDO relation between a Work Item and a Pull Request uses the
            // `ArtifactLink` rel with a `vstfs:///Git/PullRequestId/...` artifact uri.
            // The uri encodes _project-guid / repo-guid / pr-id; resolve the GUIDs
            // first so the relation lands on the right artifact.
            var gitClient = CreateGitClient();
            var repo = await gitClient.GetRepositoryAsync(_project, _repoName, cancellationToken: ct);
            var projectId = repo.ProjectReference.Id;
            var repoId = repo.Id;
            var artifactUri = $"vstfs:///Git/PullRequestId/{projectId}%2F{repoId}%2F{pullRequestId}";

            var witClient = clientFactory.CreateWorkItemClient(_organizationUrl, _personalAccessToken);
            var patch = new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchDocument
            {
                new Microsoft.VisualStudio.Services.WebApi.Patch.Json.JsonPatchOperation
                {
                    Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                    Path = "/relations/-",
                    Value = new
                    {
                        rel = "ArtifactLink",
                        url = artifactUri,
                        attributes = new { name = "Pull Request" }
                    }
                }
            };
            // p0260 audit: this PATCH bumps System.Rev too — log it through the
            // same TICKET WRITE lens so the PR-link write is never mistaken for a
            // phantom mutation when auditing who touched a ticket.
            logger.LogInformation(
                "TICKET WRITE #{WorkItem}: relations+=PR-link !{PrId} <- {Caller}",
                workItemId, pullRequestId,
                AgentSmith.Infrastructure.Services.Providers.Tickets.TicketWriteAudit.Caller());
            await witClient.UpdateWorkItemAsync(patch, workItemId, cancellationToken: ct);
            logger.LogInformation(
                "Linked work item #{WorkItem} to PR !{PrId}", workItemId, pullRequestId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to link work item #{WorkItem} to PR !{PrId} — PR is created, link is missing",
                workItemId, pullRequestId);
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
        await gitClient.CreateThreadAsync(thread, _project, _repoName, prId, cancellationToken: cancellationToken);
        logger.LogInformation("Posted comment on PR #{PrId}", prId);
    }

    private async Task<string> GetDefaultBranchAsync(CancellationToken cancellationToken)
    {
        if (_configuredDefaultBranch is not null)
            return _configuredDefaultBranch;

        if (_cachedDefaultBranch is not null)
            return _cachedDefaultBranch;

        try
        {
            var client = CreateGitClient();
            var repo = await client.GetRepositoryAsync(
                _project, _repoName, cancellationToken: cancellationToken);
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
            _project, _repoName, criteria, cancellationToken: cancellationToken)).FirstOrDefault()
            ?? throw new ProviderException(ProviderType, "PR already exists but could not be found.");
        logger.LogInformation("Found existing pull request: {Url}", BuildPrUrl(existing.PullRequestId));
        return BuildPrUrl(existing.PullRequestId);
    }

    private string BuildPrUrl(int prId) =>
        $"{_organizationUrl}/{_project}/_git/{_repoName}/pullrequest/{prId}";

    private GitHttpClient CreateGitClient() =>
        clientFactory.CreateGitClient(_organizationUrl, _personalAccessToken);

    private Task<GitHttpClient> CreateConnectionAsync(CancellationToken cancellationToken) =>
        clientFactory.CreateGitClientAsync(_organizationUrl, _personalAccessToken, cancellationToken);

    public async Task<bool> UpdatePullRequestBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken)
    {
        if (!TryParsePullRequestId(prUrl, out var prId)) return false;
        try
        {
            var client = CreateGitClient();
            await client.UpdatePullRequestAsync(
                new GitPullRequest { Description = newBody },
                _project, _repoName, prId, cancellationToken: cancellationToken);
            logger.LogInformation("Updated PR body for !{PrId}", prId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update PR body for !{PrId}", prId);
            return false;
        }
    }

    private static bool TryParsePullRequestId(string prUrl, out int prId)
    {
        prId = 0;
        var match = System.Text.RegularExpressions.Regex.Match(prUrl, @"/pullrequest/(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out prId);
    }

    public async Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken)
    {
        var client = CreateGitClient();
        var branch = await GetDefaultBranchAsync(cancellationToken);
        var descriptor = new GitVersionDescriptor
        {
            Version = branch,
            VersionType = GitVersionType.Branch
        };
        try
        {
            using var stream = await client.GetItemContentAsync(
                project: _project,
                repositoryId: _repoName,
                path: path,
                versionDescriptor: descriptor,
                cancellationToken: cancellationToken);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (VssServiceException ex) when (ex.Message.Contains("could not be found", StringComparison.OrdinalIgnoreCase)
                                          || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        var client = CreateGitClient();
        var branch = await GetDefaultBranchAsync(cancellationToken);
        var descriptor = new GitVersionDescriptor
        {
            Version = branch,
            VersionType = GitVersionType.Branch
        };
        try
        {
            // GetItemsAsync with scopePath + recursionLevel=OneLevel returns the
            // directory itself plus immediate children. Filter out the directory
            // itself; keep only the leaf names (last path segment).
            // Azure DevOps returns GitItem.Path with a leading "/" — normalise
            // both sides before the prefix match so multi-context monorepos
            // are actually listed (regression: prior code dropped every item).
            var items = await client.GetItemsAsync(
                project: _project,
                repositoryId: _repoName,
                scopePath: path,
                recursionLevel: VersionControlRecursionType.OneLevel,
                versionDescriptor: descriptor,
                cancellationToken: cancellationToken);
            logger.LogInformation(
                "ListDirectoryAsync raw: project={Project} repo={Repo} path={Path} branch={Branch} items={Count} sample=[{Sample}]",
                _project, _repoName, path, branch, items?.Count ?? 0,
                items is null
                    ? ""
                    : string.Join(", ", items.Take(8).Select(i => $"{i.Path}|folder={i.IsFolder}")));
            if (items is null || items.Count == 0) return [];

            var normPath = "/" + path.Trim('/');
            var prefix = normPath + "/";
            var result = new List<string>();
            foreach (var item in items)
            {
                if (item.Path is null) continue;
                var p = item.Path.StartsWith('/') ? item.Path : "/" + item.Path;
                if (string.Equals(p, normPath, StringComparison.Ordinal)) continue;
                string leaf;
                if (p.StartsWith(prefix, StringComparison.Ordinal))
                    leaf = p[prefix.Length..];
                else
                    leaf = p.TrimStart('/');
                if (string.IsNullOrEmpty(leaf) || leaf.Contains('/')) continue;
                if (item.IsFolder != true) continue;
                result.Add(leaf);
            }
            return result;
        }
        catch (VssServiceException ex) when (ex.Message.Contains("could not be found", StringComparison.OrdinalIgnoreCase)
                                          || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }
    }
}
