using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Commits generated .agentsmith/ files and creates a pull request.
/// Used by the init-project pipeline — no ticket needed.
/// </summary>
public sealed class InitCommitHandler(
    ISourceProviderFactory sourceFactory,
    ILogger<InitCommitHandler> logger)
    : ICommandHandler<InitCommitContext>
{
    public async Task<CommandResult> ExecuteAsync(
        InitCommitContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Committing .agentsmith/ files and creating PR...");

        var sourceProvider = sourceFactory.Create(context.SourceConfig);

        var message = "chore: initialize .agentsmith/ directory";
        await sourceProvider.CommitAndPushAsync(context.Repository, message, cancellationToken);

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
