using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Commits generated .agentsmith/ files and creates a pull request.
/// Used by the init-project pipeline — no ticket needed.
/// </summary>
public sealed class InitCommitHandler(
    ISourceProviderFactory sourceFactory,
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
        await gitOps.CommitAndPushAsync(sandbox, context.Repository.CurrentBranch.Value, message, cancellationToken);

        var prUrl = await sourceProvider.CreatePullRequestAsync(
            context.Repository,
            "Initialize .agentsmith/ directory",
            "Auto-generated project context, code map, and coding principles.",
            cancellationToken);

        context.Pipeline.Set(ContextKeys.PullRequestUrl, prUrl);

        logger.LogInformation("Init PR created: {Url}", prUrl);
        return CommandResult.Ok($"Pull request created: {prUrl}");
    }
}
