using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// The launchers a chat command starts a run through, and what they share with the other
/// doors: the manual init's admission and run rows, and the pull-request context factory.
/// Scoped like the intent handlers that use them, because the run rows are written over the
/// scoped unit of work.
/// </summary>
internal static class ChatRunLaunchExtensions
{
    internal static IServiceCollection AddChatRunLaunch(this IServiceCollection services)
    {
        services.AddProjectInit();
        services.TryAddSingleton<PrRunContextFactory>();
        services.AddScoped<ChatTicketRunLauncher>();
        services.AddScoped<ChatTicketlessRunLauncher>();
        services.AddScoped<ChatPrContextResolver>();
        services.AddScoped<ChatLaunchAnnouncer>();
        services.AddScoped<ChatRunStart>();
        return services.AddChatRunBinding();
    }
}
