using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Commits generated .agentsmith/ files and creates a pull request. When the
/// run was triggered by a labelled ticket (p0133), finalizes the ticket via
/// the shared TicketLifecycle helper — transitions to done_status (or closes
/// as fallback) and posts a PR-link summary. Slack-modal / CLI init paths
/// publish no TicketId; the lifecycle branch then no-ops.
/// </summary>
public sealed class InitCommitHandler(
    ISourceProviderFactory sourceFactory,
    ITicketProviderFactory ticketFactory,
    SandboxGitOperations gitOps,
    ILogger<InitCommitHandler> logger)
    : ICommandHandler<InitCommitContext>
{
    public async Task<CommandResult> ExecuteAsync(
        InitCommitContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Committing .agentsmith/ files and creating PR...");

        if (!context.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return CommandResult.Fail("InitCommit requires an active sandbox; none in pipeline context.");

        var sourceProvider = sourceFactory.Create(context.SourceConfig);

        var message = "chore: initialize .agentsmith/ directory";
        await gitOps.CommitAndPushAsync(
            sandbox, context.Repository.CurrentBranch.Value, message,
            context.SourceConfig.Type, cancellationToken);

        // Resolve TicketId once so the PR creation can carry the link AND the
        // post-PR lifecycle finalize sees the same id without re-reading.
        context.Pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId);

        var prUrl = await sourceProvider.CreatePullRequestAsync(
            context.Repository,
            "Initialize .agentsmith/ directory",
            "Auto-generated project context, code map, and coding principles.",
            cancellationToken,
            linkedTicketId: ticketId);

        context.Pipeline.Set(ContextKeys.PullRequestUrl, prUrl);

        logger.LogInformation("Init PR created: {Url}", prUrl);

        if (ticketId is not null)
        {
            context.Pipeline.TryGet<string>(ContextKeys.DoneStatus, out var doneStatus);

            var summary = $"""
                ## Agent Smith - Init Complete

                **PR:** {prUrl}

                Bootstrap files (`.agentsmith/context.yaml`, `coding-principles.md`) generated. Review and merge the PR to enable agent-smith pipelines on this repository.
                """;

            await TicketLifecycle.FinalizeAsync(
                ticketFactory, context.TicketConfig, ticketId,
                doneStatus, summary, logger, cancellationToken);
        }

        return CommandResult.Ok($"Pull request created: {prUrl}");
    }
}
