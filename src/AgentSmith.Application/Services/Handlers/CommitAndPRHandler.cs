using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Commits changes and creates a pull request via source provider.
/// Posts the result back to the ticket. If a DoneStatus is configured
/// (e.g. from a Jira webhook trigger), transitions the ticket to that status;
/// otherwise closes the ticket.
/// </summary>
public sealed class CommitAndPRHandler(
    ISourceProviderFactory sourceFactory,
    ITicketProviderFactory ticketFactory,
    ILogger<CommitAndPRHandler> logger)
    : ICommandHandler<CommitAndPRContext>
{
    public async Task<CommandResult> ExecuteAsync(
        CommitAndPRContext context, CancellationToken cancellationToken)
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

        await FinalizeTicketAsync(context, prUrl, cancellationToken);

        return CommandResult.Ok($"Pull request created: {prUrl}");
    }

    private async Task FinalizeTicketAsync(
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

            if (context.Pipeline.TryGet<string>(ContextKeys.DoneStatus, out var doneStatus)
                && !string.IsNullOrWhiteSpace(doneStatus))
            {
                await ticketProvider.UpdateStatusAsync(
                    context.Ticket.Id, summary, cancellationToken);
                await ticketProvider.TransitionToAsync(
                    context.Ticket.Id, doneStatus, cancellationToken);

                logger.LogInformation(
                    "Ticket {Ticket} transitioned to '{DoneStatus}' with summary",
                    context.Ticket.Id, doneStatus);
            }
            else
            {
                await ticketProvider.CloseTicketAsync(
                    context.Ticket.Id, summary, cancellationToken);

                logger.LogInformation("Ticket {Ticket} closed with summary", context.Ticket.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to finalize ticket {Ticket}, PR was still created", context.Ticket.Id);
        }
    }
}
