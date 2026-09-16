using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Slack + Teams + dashboard chat adapters. Each builds a typed HttpClient with a 30s timeout
/// matching the platform's regional routing, wires the per-platform message / card
/// builders + progress formatter, and registers the adapter as a Singleton —
/// exposed through IPlatformAdapter so the dispatcher resolves all adapters
/// uniformly. Teams additionally registers Bot Framework token + JWT validator
/// + interaction handler.
/// </summary>
internal static class ChatAdaptersExtensions
{
    /// <summary>
    /// 2026-09-15-9033: the dashboard's own spec-dialog channel. Registered BEFORE the two
    /// chat adapters on purpose: eight classes still take a single IPlatformAdapter and a
    /// single-service resolve yields the LAST registration, so a third one added at the end
    /// would silently move the run-trigger chat path onto a hub group.
    /// </summary>
    internal static IServiceCollection AddDashboardAdapter(this IServiceCollection services)
    {
        services.AddSingleton<DashboardAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<DashboardAdapter>());
        return services;
    }

    internal static IServiceCollection AddTeamsAdapter(this IServiceCollection services)
    {
        var options = new TeamsAdapterOptions
        {
            AppId = Environment.GetEnvironmentVariable("TEAMS_APP_ID") ?? string.Empty,
            AppPassword = Environment.GetEnvironmentVariable("TEAMS_APP_PASSWORD") ?? string.Empty,
            TenantId = Environment.GetEnvironmentVariable("TEAMS_TENANT_ID") ?? string.Empty,
        };
        services.AddSingleton(options);
        services.AddSingleton(new TeamsJwtValidator(options.AppId));
        services.AddTransient<TeamsQuestionCardBuilder>();
        services.AddTransient<TeamsStatusCardBuilder>();
        services.AddTransient<TeamsCardBuilder>();
        services.AddHttpClient<BotFrameworkTokenProvider>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<TeamsApiClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<TeamsTypedQuestionTracker>();
        services.AddSingleton<TeamsAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<TeamsAdapter>());
        services.AddScoped<TeamsInteractionHandler>();
        return services;
    }

    internal static IServiceCollection AddSlackAdapter(this IServiceCollection services)
    {
        services.AddSingleton(new SlackAdapterOptions
        {
            BotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? string.Empty,
            SigningSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? string.Empty
        });
        services.AddHttpClient<SlackApiClient>(c =>
        {
            c.BaseAddress = new Uri("https://slack.com/api/");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddTransient<SlackTypedQuestionBlockBuilder>();
        services.AddTransient<SlackMessageBlockBuilder>();
        services.AddTransient<SlackProgressFormatter>();
        services.AddSingleton<SlackAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<SlackAdapter>());
        return services;
    }
}
