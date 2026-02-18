using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Commands.Handlers;

/// <summary>
/// Commits changes and creates a pull request via source provider.
/// Posts the result back to the ticket and closes it.
/// </summary>
public sealed class CommitAndPRHandler(
    ISourceProviderFactory sourceFactory,
    ITicketProviderFactory ticketFactory,
    ILogger<CommitAndPRHandler> logger)
    : ICommandHandler<CommitAndPRContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CommitAndPRContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Creating PR for ticket {Ticket} with {Changes} changes...",
            context.Ticket.Id, context.Changes.Count);

        var sourceProvider = sourceFactory.Create(context.SourceConfig);

        var message = $"fix: {context.Ticket.Title} (#{context.Ticket.Id})";
        await sourceProvider.CommitAndPushAsync(context.Repository, message, cancellationToken);

        var prUrl = await sourceProvider.CreatePullRequestAsync(
            context.Repository,
            context.Ticket.Title,
            context.Ticket.Description,
            cancellationToken);

        context.Pipeline.Set(ContextKeys.PullRequestUrl, prUrl);

        logger.LogInformation("Pull request created: {Url}", prUrl);

        await CloseTicketWithSummaryAsync(context, prUrl, cancellationToken);

        return CommandResult.Ok($"Pull request created: {prUrl}");
    }

    private async Task CloseTicketWithSummaryAsync(
        CommitAndPRContext context, string prUrl, CancellationToken cancellationToken)
    {
        try
        {
            var ticketProvider = ticketFactory.Create(context.TicketConfig);
            var changes = string.Join("\n",
                context.Changes.Select(c => $"- [{c.ChangeType}] `{c.Path}`"));

            var summary = $"""
                ## Agent Smith - Completed

                **PR:** {prUrl}

                ### Changes
                {changes}

                This ticket was automatically processed by Agent Smith.
                """;

            await ticketProvider.CloseTicketAsync(
                context.Ticket.Id, summary, cancellationToken);

            logger.LogInformation("Ticket {Ticket} closed with summary", context.Ticket.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to close ticket {Ticket}, PR was still created", context.Ticket.Id);
        }
    }
}
