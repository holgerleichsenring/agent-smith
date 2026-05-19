using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

internal static partial class ServiceCollectionExtensions
{
    // Teams adapter: Bot Framework token provider + Teams API client (typed HttpClients
    // via IHttpClientFactory, 30 s timeout aligns with Bot Framework regional routing).
    // Service-URL is resolved per conversation in TeamsApiClient — no BaseAddress is set.
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

    // Slack adapter: typed HttpClient with 30 s timeout for slack.com/api/* calls,
    // block builders + progress formatter, and the IPlatformAdapter wire-up.
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
