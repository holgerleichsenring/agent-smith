using AgentSmith.Application;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Health;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Lifecycle;
using AgentSmith.Infrastructure.Services.Queue;
using AgentSmith.Infrastructure.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Cli;

internal static class ServiceProviderFactory
{
    public static ServiceProvider Build(
        bool verbose, bool headless, string jobId, string redisUrl,
        string? configPath = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConsoleFormatter<ShortCategoryFormatter, ShortCategoryFormatterOptions>();
            builder.AddConsole(options => options.FormatterName = "short");
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
        });
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        RegisterWebhookHandlers(services);
        RegisterRedis(services, jobId, redisUrl);
        RegisterProgressReporter(services, headless, jobId, redisUrl);

        if (configPath is not null)
            services.AddSingleton(new ServerContext(configPath));

        return services.BuildServiceProvider();
    }

    private static void RegisterWebhookHandlers(IServiceCollection services)
    {
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitHubIssueWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitHubIssueCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitHubPrLabelWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitLabMrLabelWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitLabIssueWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitLabIssueCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.AzureDevOpsWorkItemWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.AzureDevOpsWorkItemCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitHubPrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitLabMrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.AzureDevOpsPrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.JiraAssigneeWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.JiraCommentWebhookHandler>();
    }

    private static void RegisterRedis(IServiceCollection services, string jobId, string redisUrl)
    {
        var resolvedUrl = string.IsNullOrWhiteSpace(redisUrl)
            ? Environment.GetEnvironmentVariable("REDIS_URL")
            : redisUrl;

        if (string.IsNullOrWhiteSpace(resolvedUrl))
        {
            services.AddSingleton<ISubsystemHealth>(DisabledHealth("redis", "REDIS_URL not configured"));
            services.Replace(ServiceDescriptor.Scoped<ITicketClaimService, NullTicketClaimService>());
            return;
        }

        var multiplexer = ConnectMultiplexer(resolvedUrl);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<IRedisJobQueue, RedisJobQueue>();
        services.AddSingleton<IRedisClaimLock, RedisClaimLock>();
        services.AddSingleton<IRedisLeaderLease, RedisLeaderLease>();
        services.AddSingleton<IJobHeartbeatService, JobHeartbeatService>();
        services.AddSingleton<IConversationLookup, RedisConversationLookup>();
        services.AddSingleton<IMessageBus, RedisMessageBus>();
        services.AddSingleton<IDialogueTransport, RedisDialogueTransport>();
        services.AddSingleton<RedisConnectionHealth>();
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<RedisConnectionHealth>().Health);
    }

    private static IConnectionMultiplexer ConnectMultiplexer(string redisUrl)
    {
        var options = ConfigurationOptions.Parse(redisUrl);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(options);
    }

    private static ISubsystemHealth DisabledHealth(string name, string reason)
    {
        var h = new SubsystemHealth(name);
        h.SetDisabled(reason);
        return h;
    }

    private static void RegisterProgressReporter(
        IServiceCollection services, bool headless, string jobId, string redisUrl)
    {
        if (!string.IsNullOrWhiteSpace(jobId) && !string.IsNullOrWhiteSpace(redisUrl))
        {
            services.AddSingleton<IProgressReporter>(sp =>
                new RedisProgressReporter(
                    sp.GetRequiredService<IMessageBus>(),
                    jobId,
                    sp.GetRequiredService<ILogger<RedisProgressReporter>>()));
            return;
        }

        services.AddSingleton<IProgressReporter>(sp =>
            new ConsoleProgressReporter(
                sp.GetRequiredService<ILogger<ConsoleProgressReporter>>(), headless));

        services.AddSingleton<IDialogueTransport>(sp =>
            new ConsoleDialogueTransport(
                Console.In,
                Console.Out,
                sp.GetRequiredService<ILogger<ConsoleDialogueTransport>>()));
    }
}
