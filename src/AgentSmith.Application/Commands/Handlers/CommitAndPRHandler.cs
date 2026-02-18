using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Commands.Handlers;

/// <summary>
/// Commits changes and creates a pull request via source provider.
/// </summary>
public sealed class CommitAndPRHandler(
    ISourceProviderFactory factory,
    ILogger<CommitAndPRHandler> logger)
    : ICommandHandler<CommitAndPRContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CommitAndPRContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Creating PR for ticket {Ticket} with {Changes} changes...",
            context.Ticket.Id, context.Changes.Count);

        var provider = factory.Create(context.SourceConfig);

        var message = $"fix: {context.Ticket.Title} (#{context.Ticket.Id})";
        await provider.CommitAndPushAsync(context.Repository, message, cancellationToken);

        var prUrl = await provider.CreatePullRequestAsync(
            context.Repository,
            context.Ticket.Title,
            context.Ticket.Description,
            cancellationToken);

        context.Pipeline.Set(ContextKeys.PullRequestUrl, prUrl);

        logger.LogInformation("Pull request created: {Url}", prUrl);
        return CommandResult.Ok($"Pull request created: {prUrl}");
    }
}
