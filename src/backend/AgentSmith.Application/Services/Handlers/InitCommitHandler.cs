using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Commits generated .agentsmith/ files per repo and creates an init pull
/// request per repo. Iterates Configs: per repo detect staged changes (skip
/// if none — bootstrap on an already-initialized repo is a no-op), commit,
/// push, open PR with a sibling-PR marker in the body that p0158c later
/// PATCHes with sibling URLs. Publishes ContextKeys.OpenedPullRequests for
/// multi-repo runs; single-repo runs also publish ContextKeys.PullRequestUrl
/// for backward compatibility. When a TicketId is present, finalizes the
/// ticket via the shared TicketLifecycle helper.
/// </summary>
public sealed class InitCommitHandler(
    ISourceProviderFactory sourceFactory,
    ITicketProviderFactory ticketFactory,
    SandboxGitOperations gitOps,
    ILogger<InitCommitHandler> logger)
    : ICommandHandler<InitCommitContext>
{
    private const string SiblingMarker = "<!-- agentsmith:sibling-prs -->";

    public async Task<CommandResult> ExecuteAsync(
        InitCommitContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Committing .agentsmith/ files across {Repos} repo(s)...",
            context.Configs.Count);

        context.Pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId);

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
            var sandbox = matches[0].Value;
            // p0299: mixed-stack monorepo — fold every other toolchain sandbox's edits into
            // the primary before commit (was: committed matches[0] only, dropping the rest).
            var consolidated = await gitOps.ConsolidateSecondarySandboxesAsync(matches, sandbox, cancellationToken);
            if (consolidated > 0)
                logger.LogInformation("{Repo}: consolidated {N} secondary sandbox(es) into {Key}",
                    repo.Name, consolidated, matches[0].Key);
            var (result, body) = await OpenOneAsync(context, sandbox, repo, ticketId, cancellationToken);
            opened.Add(result);
            if (body is not null) bodies[repo.Name] = body;
        }

        context.Pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, opened);
        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.OpenedPullRequestBodies, bodies);
        var primaryUrl = opened.FirstOrDefault(o => o.Status == OpenStatus.Opened)?.Url;
        if (primaryUrl is not null)
            context.Pipeline.Set(ContextKeys.PullRequestUrl, primaryUrl);

        // p0321: finalize on every ticketed run that isn't a hard failure — the PR
        // is init's output, not its success criterion. Gating on primaryUrl left a
        // no-change re-init (all repos SkippedNoChanges) in trigger_statuses, so
        // the tracker poller re-claimed the ticket every cycle forever. A total
        // failure still skips finalize and leaves the ticket to the error lifecycle.
        var commandResult = BuildResult(opened);
        if (ticketId is not null && commandResult.IsSuccess)
            await FinalizeTicketAsync(context, opened, ticketId, cancellationToken);
        return commandResult;
    }

    private async Task<(OpenedPullRequest Result, string? Body)> OpenOneAsync(
        InitCommitContext context, ISandbox sandbox, RepoConnection repo,
        TicketId? ticketId, CancellationToken ct)
    {
        try
        {
            await gitOps.CommitAndPushAsync(
                sandbox, context.Repository.CurrentBranch.Value,
                "chore: initialize .agentsmith/ directory", repo.Type, ct);
        }
        catch (Exception ex) when (LooksLikeEmptyCommit(ex))
        {
            logger.LogInformation("{Repo}: no init changes, skipping PR", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges), null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: init commit/push failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed), null);
        }

        var body = $"Auto-generated project context, code map, and coding principles.\n\n{SiblingMarker}";
        try
        {
            var provider = sourceFactory.Create(repo);
            var prUrl = await provider.CreatePullRequestAsync(
                new Repository(context.Repository.CurrentBranch, repo.Url ?? string.Empty),
                "Initialize .agentsmith/ directory", body, ct, linkedTicketId: ticketId);
            logger.LogInformation("{Repo}: init PR opened {Url}", repo.Name, prUrl);
            return (new OpenedPullRequest(repo.Name, prUrl, OpenStatus.Opened), body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: init PR open failed", repo.Name);
            return (new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed), null);
        }
    }

    private static bool LooksLikeEmptyCommit(Exception ex) =>
        ex.Message.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("no changes", StringComparison.OrdinalIgnoreCase);

    private Task FinalizeTicketAsync(
        InitCommitContext context, IReadOnlyList<OpenedPullRequest> opened,
        TicketId ticketId, CancellationToken ct)
    {
        context.Pipeline.TryGet<string>(ContextKeys.DoneStatus, out var doneStatus);
        // p0321: a no-change re-init is a success outcome without a PR — say so
        // instead of rendering "(no changes)" bullets under a "Pull requests" heading.
        var summary = opened.Any(o => o.Status == OpenStatus.Opened)
            ? $"""
                ## Agent Smith - Init Complete across {context.Configs.Count} repo(s)

                ### Pull requests
                {RenderPullRequestList(opened)}

                Bootstrap files (`.agentsmith/context.yaml`, `coding-principles.md`) generated. Review and merge to enable agent-smith pipelines.
                """
            : $"""
                ## Agent Smith - Init Complete across {context.Configs.Count} repo(s)

                No changes — project already bootstrapped and context up to date; no pull request needed.
                """;
        return TicketLifecycle.FinalizeAsync(
            ticketFactory, context.TrackerConnection, ticketId, doneStatus, summary, logger, ct);
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
            return CommandResult.Fail($"All {opened.Count} init PR open attempts failed.");
        // p0321: all repos skipped — an already-bootstrapped project is a successful
        // no-op, not a missing PR.
        if (openedEntries.Count == 0)
            return CommandResult.Ok("No changes — project already bootstrapped, no pull request needed.");
        if (openedEntries.Count == 1)
            return CommandResult.Ok($"Pull request created: {openedEntries[0].Url}");
        var urls = string.Join(", ", openedEntries.Select(o => o.Url));
        return CommandResult.Ok($"Pull requests created: {urls}");
    }
}
