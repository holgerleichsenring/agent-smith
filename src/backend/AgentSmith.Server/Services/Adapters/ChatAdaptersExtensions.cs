using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Slack + Teams + dashboard chat adapters. Each builds a typed HttpClient with a 30s timeout
/// matching the platform's regional routing, wires the per-platform message / card
/// builders, and registers the adapter as a Singleton — exposed through IPlatformAdapter
/// so PlatformAdapters and the spec-dialog messenger resolve all adapters by platform. Teams additionally registers Bot Framework token + JWT validator
/// + interaction handler.
/// </summary>
internal static class ChatAdaptersExtensions
{
    /// <summary>
    /// 2026-09-15-9033: the dashboard's own spec-dialog channel. No class takes a single
    /// IPlatformAdapter any more — every reply names its platform through PlatformAdapters —
    /// so the order of the three registrations decides nothing.
    /// </summary>
    internal static IServiceCollection AddDashboardAdapter(this IServiceCollection services)
    {
        services.AddSingleton<DashboardAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<DashboardAdapter>());
        return services;
    }

    internal static IServiceCollection AddTeamsAdapter(this IServiceCollection services)
    {
        var appId = Environment.GetEnvironmentVariable("TEAMS_APP_ID") ?? string.Empty;
        services.AddSingleton(sp => new TeamsAdapterOptions(sp.GetRequiredService<ICredentialResolver>().Named)
        {
            AppId = appId,
            TenantId = Environment.GetEnvironmentVariable("TEAMS_TENANT_ID") ?? string.Empty,
        });
        services.AddSingleton(new TeamsJwtValidator(appId));
        services.AddTransient<TeamsQuestionCardBuilder>();
        services.AddTransient<TeamsStatusCardBuilder>();
        services.AddTransient<TeamsCardBuilder>();
        services.AddHttpClient<BotFrameworkTokenProvider>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<TeamsApiClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<TeamsServiceUrls>();
        services.AddSingleton<TeamsTypedQuestionTracker>();
        services.AddSingleton<TeamsAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<TeamsAdapter>());
        services.AddTransient<IChatThreadAdapter, TeamsThreadAdapter>();
        services.AddScoped<TeamsInteractionHandler>();
        return services;
    }

    internal static IServiceCollection AddSlackAdapter(this IServiceCollection services)
    {
        // 2026-11-18-9ae8: the credentials are read per use, environment first, then the secret store.
        services.AddSingleton(sp => new SlackAdapterOptions(sp.GetRequiredService<ICredentialResolver>().Lookup));
        services.AddHttpClient<SlackApiClient>(c =>
        {
            c.BaseAddress = new Uri("https://slack.com/api/");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddTransient<SlackTypedQuestionBlockBuilder>();
        services.AddTransient<SlackMessageBlockBuilder>();
        services.AddSingleton<SlackAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<SlackAdapter>());
        services.AddTransient<IChatThreadAdapter, SlackThreadAdapter>();
        return services;
    }
}
