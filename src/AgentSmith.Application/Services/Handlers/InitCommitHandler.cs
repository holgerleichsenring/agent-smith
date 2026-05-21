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

        if (!context.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return CommandResult.Fail("InitCommit requires an active sandbox; none in pipeline context.");

        context.Pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId);

        var opened = new List<OpenedPullRequest>(context.Configs.Count);
        foreach (var repo in context.Configs)
            opened.Add(await OpenOneAsync(context, sandbox, repo, ticketId, cancellationToken));

        context.Pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.OpenedPullRequests, opened);
        var primaryUrl = opened.FirstOrDefault(o => o.Status == OpenStatus.Opened)?.Url;
        if (primaryUrl is not null)
            context.Pipeline.Set(ContextKeys.PullRequestUrl, primaryUrl);

        if (ticketId is not null && primaryUrl is not null)
            await FinalizeTicketAsync(context, primaryUrl, ticketId, cancellationToken);
        return BuildResult(opened);
    }

    private async Task<OpenedPullRequest> OpenOneAsync(
        InitCommitContext context, ISandbox sandbox, RepoConnection repo,
        TicketId? ticketId, CancellationToken ct)
    {
        var workdir = Repository.WorkPathFor(repo.Name);
        try
        {
            await gitOps.CommitAndPushAsync(
                sandbox, context.Repository.CurrentBranch.Value,
                "chore: initialize .agentsmith/ directory", repo.Type, workdir, ct);
        }
        catch (Exception ex) when (LooksLikeEmptyCommit(ex))
        {
            logger.LogInformation("{Repo}: no init changes, skipping PR", repo.Name);
            return new OpenedPullRequest(repo.Name, Url: null, OpenStatus.SkippedNoChanges);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: init commit/push failed", repo.Name);
            return new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed);
        }

        try
        {
            var provider = sourceFactory.Create(repo);
            var body = $"Auto-generated project context, code map, and coding principles.\n\n{SiblingMarker}";
            var prUrl = await provider.CreatePullRequestAsync(
                new Repository(context.Repository.CurrentBranch, repo.Url ?? string.Empty, workdir),
                "Initialize .agentsmith/ directory", body, ct, linkedTicketId: ticketId);
            logger.LogInformation("{Repo}: init PR opened {Url}", repo.Name, prUrl);
            return new OpenedPullRequest(repo.Name, prUrl, OpenStatus.Opened);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Repo}: init PR open failed", repo.Name);
            return new OpenedPullRequest(repo.Name, Url: null, OpenStatus.Failed);
        }
    }

    private static bool LooksLikeEmptyCommit(Exception ex) =>
        ex.Message.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("no changes", StringComparison.OrdinalIgnoreCase);

    private Task FinalizeTicketAsync(
        InitCommitContext context, string primaryUrl, TicketId ticketId, CancellationToken ct)
    {
        context.Pipeline.TryGet<string>(ContextKeys.DoneStatus, out var doneStatus);
        var summary = $"""
            ## Agent Smith - Init Complete across {context.Configs.Count} repo(s)

            **Primary PR:** {primaryUrl}

            Bootstrap files (`.agentsmith/context.yaml`, `coding-principles.md`) generated. Review and merge to enable agent-smith pipelines.
            """;
        return TicketLifecycle.FinalizeAsync(
            ticketFactory, context.TrackerConnection, ticketId, doneStatus, summary, logger, ct);
    }

    private static CommandResult BuildResult(IReadOnlyList<OpenedPullRequest> opened)
    {
        var openedEntries = opened.Where(o => o.Status == OpenStatus.Opened).ToList();
        var failed = opened.Count(o => o.Status == OpenStatus.Failed);
        if (openedEntries.Count == 0 && failed > 0)
            return CommandResult.Fail($"All {opened.Count} init PR open attempts failed.");
        if (openedEntries.Count == 1)
            return CommandResult.Ok($"Pull request created: {openedEntries[0].Url}");
        var urls = string.Join(", ", openedEntries.Select(o => o.Url));
        return CommandResult.Ok($"Pull requests created: {urls}");
    }
}
