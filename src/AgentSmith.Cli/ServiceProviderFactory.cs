using AgentSmith.Application;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Cli.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Services.Bus;
using Microsoft.Extensions.DependencyInjection;
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
        RegisterProgressReporter(services, headless, jobId, redisUrl);

        if (configPath is not null)
            services.AddSingleton(new ServerContext(configPath));

        return services.BuildServiceProvider();
    }

    private static void RegisterWebhookHandlers(IServiceCollection services)
    {
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitHubIssueWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitHubPrLabelWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitLabMrLabelWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.AzureDevOpsWorkItemWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitHubPrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.GitLabMrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.AzureDevOpsPrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, Services.Webhooks.JiraAssigneeWebhookHandler>();
    }

    private static void RegisterProgressReporter(
        IServiceCollection services, bool headless, string jobId, string redisUrl)
    {
        if (!string.IsNullOrWhiteSpace(jobId) && !string.IsNullOrWhiteSpace(redisUrl))
        {
            var redis = ConnectionMultiplexer.Connect(redisUrl);
            services.AddSingleton<IConnectionMultiplexer>(redis);
            services.AddSingleton<IMessageBus, RedisMessageBus>();
            services.AddSingleton<IProgressReporter>(sp =>
                new RedisProgressReporter(
                    sp.GetRequiredService<IMessageBus>(),
                    jobId,
                    sp.GetRequiredService<ILogger<RedisProgressReporter>>()));
        }
        else
        {
            services.AddSingleton<IProgressReporter>(sp =>
                new ConsoleProgressReporter(
                    sp.GetRequiredService<ILogger<ConsoleProgressReporter>>(), headless));

            // Override the Infrastructure RedisDialogueTransport with interactive console I/O
            services.AddSingleton<IDialogueTransport>(sp =>
                new ConsoleDialogueTransport(
                    Console.In,
                    Console.Out,
                    sp.GetRequiredService<ILogger<ConsoleDialogueTransport>>()));
        }
    }
}
