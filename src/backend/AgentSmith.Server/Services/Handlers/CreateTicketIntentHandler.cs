using AgentSmith.Server.Contracts;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Handlers;

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
    public async Task HandleAsync(CreateTicketIntent intent, CancellationToken cancellationToken)
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

            var ticketProvider = ticketFactory.Create(projectConfig.Tracker);
            var created = await ticketProvider.CreateAsync(
                intent.Title,
                intent.Description ?? string.Empty,
                labels: [],
                cancellationToken);

            await SendConfirmationAsync(intent, created, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create ticket in project {Project}", intent.Project);
            await adapter.SendMessageAsync(intent.ChannelId, $":x: {ex.Message}", cancellationToken);
        }
    }

    private async Task SendConfirmationAsync(
        CreateTicketIntent intent,
        CreatedTicket created,
        CancellationToken cancellationToken)
    {
        var link = created.WebUrl is null ? string.Empty : $"\n:link: {created.WebUrl}";
        await adapter.SendMessageAsync(
            intent.ChannelId,
            $":white_check_mark: Ticket *#{created.Id.Value}* created in *{intent.Project}*: _{intent.Title}_{link}\n" +
            $"To start working on it: `fix #{created.Id.Value} in {intent.Project}`",
            cancellationToken);
    }
}
