using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AgentSmith.Server.Services.SpecDialog.RunMessages;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>2026-11-17-4860b: a ticket conversation's messages to a running code attempt — the router's divert and the
/// port the run reads them through.</summary>
internal static class TicketRunMessagesExtensions
{
    internal static IServiceCollection AddTicketRunMessages(this IServiceCollection services)
    {
        services.AddScoped<SpecDialogRunDivert>().AddScoped<TicketRunningAttempt>();
        services.TryAddSingleton<ITicketConversationMessages, TicketConversationMessages>();
        return services;
    }
}
