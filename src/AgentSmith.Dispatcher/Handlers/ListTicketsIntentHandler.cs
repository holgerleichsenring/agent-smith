using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Dispatcher.Adapters;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Handlers;

/// <summary>
/// Handles the ListTicketsIntent: loads open tickets from the configured provider
/// and posts a formatted list to the chat channel.
/// Executed directly by the Dispatcher — no K8s Job required.
/// </summary>
public sealed class ListTicketsIntentHandler(
    IPlatformAdapter adapter,
    IConfigurationLoader configLoader,
    ITicketProviderFactory ticketFactory,
    ILogger<ListTicketsIntentHandler> logger)
{
    private const int MaxTicketsDisplayed = 20;

    public async Task HandleAsync(ListTicketsIntent intent, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);

            if (!config.Projects.TryGetValue(intent.Project, out var projectConfig))
            {
                await adapter.SendMessageAsync(
                    intent.ChannelId,
                    $":x: Project *{intent.Project}* not found in configuration.",
                    cancellationToken);
                return;
            }

            var ticketProvider = ticketFactory.Create(projectConfig.Tickets);
            var tickets = await ticketProvider.ListOpenAsync(cancellationToken: cancellationToken);

            await SendTicketListAsync(intent, tickets, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list tickets for project {Project}", intent.Project);
            await adapter.SendErrorAsync(intent.ChannelId, ex.Message, cancellationToken);
        }
    }

    private async Task SendTicketListAsync(
        ListTicketsIntent intent,
        IReadOnlyList<AgentSmith.Domain.Entities.Ticket> tickets,
        CancellationToken cancellationToken)
    {
        if (!tickets.Any())
        {
            await adapter.SendMessageAsync(
                intent.ChannelId,
                $":white_check_mark: No open tickets found in *{intent.Project}*.",
                cancellationToken);
            return;
        }

        var lines = tickets
            .Take(MaxTicketsDisplayed)
            .Select(t => $"• *#{t.Id}* — {t.Title} `[{t.Status}]`");

        var text = $":ticket: *Open tickets in {intent.Project}* ({tickets.Count} total):\n"
                   + string.Join("\n", lines);

        await adapter.SendMessageAsync(intent.ChannelId, text, cancellationToken);
    }
}
