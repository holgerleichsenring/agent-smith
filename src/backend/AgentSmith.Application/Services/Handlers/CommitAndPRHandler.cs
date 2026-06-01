using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Commits changes per repo (in the sandbox where the modifications live) and
/// opens a pull request via the source provider's API. Per repo: detect staged
/// changes (skip if none), commit + push, open PR with a sibling-PR marker in
/// the body that p0158c's PATCH pass replaces with actual sibling URLs. Each
/// outcome is recorded in ContextKeys.OpenedPullRequests; single-PR runs also
/// publish ContextKeys.PullRequestUrl for backward compatibility with the
/// pipeline executor result adapter. Ticket lifecycle finalization runs after
/// all repos have been processed and references the primary PR URL.
/// </summary>
public sealed class CommitAndPRHandler(
    ISourceProviderFactory sourceFactory,
    ITicketProviderFactory ticketFactory,
    SandboxGitOperations gitOps,
    ISecretPatternScanner secretScanner,
    ILogger<CommitAndPRHandler> logger)
    : ICommandHandler<CommitAndPRContext>
{
    private const string SiblingMarker = "<!-- agentsmith:sibling-prs -->";

    public async Task<CommandResult> ExecuteAsync(
        CommitAndPRContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Creating PRs for ticket {Ticket} across {Repos} repo(s)...",
            context.Ticket.Id, context.Configs.Count);

        if (!context.Pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
                ContextKeys.Sandboxes, out var sandboxes) || sandboxes is null)
            return CommandResult.Fail("CommitAndPR requires Sandboxes published by PipelineSandboxCoordinator.");

        var opened = new List<OpenedPullRequest>(context.Configs.Count);
        var bodies = new Dictionary<string, string>(context.Configs.Count, StringComparer.Ordinal);
        foreach (var repo in context.Configs)
        {
            var matches = SandboxTargets.SandboxesForRepo(context.Pipeline, repo);
            if (matches.Count == 0)
            {
                opened.Add(new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed));
                logger.LogWarning("{Repo}: no sandbox available", repo.Name);
                continue;
            }
            // Multi-context monorepo: PR from the first sandbox of the repo. Per-context
            // commit aggregation is a follow-up if multi-context edits land in one repo.
            var (result, body) = await OpenOneAsync(context, matches[0].Value, repo, cancellationToken);
            opened.Add(result);
            if (body is not null) bodies[repo.Name] = body;
        }

        context.Pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, opened);
        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies, bodies);
        var primaryUrl = opened.FirstOrDefault(o => o.Status == OpenStatus.Opened)?.Url;
        if (primaryUrl is not null)
            context.Pipeline.Set(ContextKeys.PullRequestUrl, primaryUrl);

        await FinalizeTicketAsync(context, opened, cancellationToken);
        return BuildResult(opened);
    }

    private async Task<(OpenedPullRequest Result, string? Body)> OpenOneAsync(
        CommitAndPRContext context, ISandbox sandbox, RepoConnection repo, CancellationToken ct)
    {
        var branch = context.Repository.CurrentBranch.Value;
        var message = $"fix: {context.Ticket.Title} (#{context.Ticket.Id})";
        try
        {
            await gitOps.StageAllAsync(sandbox, ct);
            var leak = await ScanStagedDiffAsync(sandbox, repo.Name, ct);
            if (leak is not null)
            {
                logger.LogError("{Repo}: secret-pattern match in staged diff at {Where} — aborting commit", repo.Name, leak);
                return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed), null);
            }
            await gitOps.CommitAndPushStagedAsync(sandbox, branch, message, repo.Type, ct);
        }
        catch (Exception ex) when (LooksLikeEmptyCommit(ex))
        {
            logger.LogInformation("{Repo}: no changes, skipping PR", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges), null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: commit/push failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed), null);
        }

        var body = $"{context.Ticket.Description}\n\n{SiblingMarker}";
        try
        {
            var provider = sourceFactory.Create(repo);
            var prUrl = await provider.CreatePullRequestAsync(
                new Repository(context.Repository.CurrentBranch, repo.Url ?? string.Empty),
                context.Ticket.Title, body, ct, linkedTicketId: context.Ticket.Id);
            logger.LogInformation("{Repo}: PR opened {Url}", repo.Name, prUrl);
            return (new OpenedPullRequest(repo.Name, prUrl, OpenStatus.Opened), body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: PR open failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed), null);
        }
    }

    private static bool LooksLikeEmptyCommit(Exception ex) =>
        ex.Message.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("no changes", StringComparison.OrdinalIgnoreCase);

    // p0192: defence-in-depth around the master-prompt rule from p0191. The
    // agent is instructed to apply credentials at user-config level
    // (~/.nuget/...), never to the repo's own files — this scan is the
    // gate that runs anyway, in case the rule is ignored. First match wins;
    // the operator sees the file:line in the failure log.
    private async Task<string?> ScanStagedDiffAsync(ISandbox sandbox, string repoName, CancellationToken ct)
    {
        var diff = await gitOps.GetStagedDiffAsync(sandbox, ct);
        if (string.IsNullOrEmpty(diff)) return null;
        var matches = secretScanner.Scan($"{repoName}-staged-diff", diff);
        return matches.Count == 0 ? null : $"line {matches[0].Line} ({matches[0].Pattern})";
    }

    private Task FinalizeTicketAsync(
        CommitAndPRContext context, IReadOnlyList<OpenedPullRequest> opened, CancellationToken ct)
    {
        if (!opened.Any(o => o.Status == OpenStatus.Opened)) return Task.CompletedTask;

        var changes = string.Join("\n",
            context.Changes.Select(c => $"- [{c.ChangeType}] `{c.Path}`"));
        var summary = $"""
            ## Agent Smith - Completed across {context.Configs.Count} repo(s)

            ### Pull requests
            {RenderPullRequestList(opened)}

            ### Changes
            {changes}

            This ticket was automatically processed by Agent Smith.
            """;
        context.Pipeline.TryGet<string>(ContextKeys.DoneStatus, out var doneStatus);
        return TicketLifecycle.FinalizeAsync(
            ticketFactory, context.TrackerConnection, context.Ticket.Id,
            doneStatus, summary, logger, ct);
    }

    private static string RenderPullRequestList(IReadOnlyList<OpenedPullRequest> opened) =>
        string.Join("\n", opened.Select(o => o.Status switch
        {
            OpenStatus.Opened => $"- **{o.RepoName}**: {o.Url}",
            OpenStatus.SkippedNoChanges => $"- **{o.RepoName}**: _(no changes)_",
            _ => $"- **{o.RepoName}**: _(open failed)_",
        }));

    private static CommandResult BuildResult(IReadOnlyList<OpenedPullRequest> opened)
    {
        var openedEntries = opened.Where(o => o.Status == OpenStatus.Opened).ToList();
        var failed = opened.Count(o => o.Status == OpenStatus.Failed);
        if (openedEntries.Count == 0 && failed > 0)
            return CommandResult.Fail($"All {opened.Count} PR open attempts failed.");
        if (openedEntries.Count == 1)
            return CommandResult.Ok($"Pull request created: {openedEntries[0].Url}");
        var urls = string.Join(", ", openedEntries.Select(o => o.Url));
        return CommandResult.Ok($"Pull requests created: {urls}");
    }
}
