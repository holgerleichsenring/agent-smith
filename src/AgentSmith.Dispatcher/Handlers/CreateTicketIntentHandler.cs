using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Dispatcher.Adapters;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Handlers;

/// <summary>
/// Handles the CreateTicketIntent: creates a new ticket in the configured provider
/// and posts a confirmation with the new ticket ID to the chat channel.
/// Executed directly by the Dispatcher — no K8s Job required.
/// </summary>
public sealed class CreateTicketIntentHandler(
    IPlatformAdapter adapter,
    IConfigurationLoader configLoader,
    ITicketProviderFactory ticketFactory,
    ILogger<CreateTicketIntentHandler> logger)
{
    public async Task HandleAsync(CreateTicketIntent intent, CancellationToken cancellationToken = default)
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
            var ticketId = await ticketProvider.CreateAsync(
                intent.Title,
                intent.Description ?? string.Empty,
                cancellationToken);

            await SendConfirmationAsync(intent, ticketId.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create ticket in project {Project}", intent.Project);
            await adapter.SendMessageAsync(intent.ChannelId, $":x: {ex.Message}", cancellationToken);
        }
    }

    private async Task SendConfirmationAsync(
        CreateTicketIntent intent,
        string ticketId,
        CancellationToken cancellationToken)
    {
        await adapter.SendMessageAsync(
            intent.ChannelId,
            $":white_check_mark: Ticket *#{ticketId}* created in *{intent.Project}*: _{intent.Title}_\n" +
            $"To start working on it: `fix #{ticketId} in {intent.Project}`",
            cancellationToken);
    }
}
