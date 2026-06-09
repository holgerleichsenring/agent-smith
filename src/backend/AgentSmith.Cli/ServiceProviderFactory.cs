using AgentSmith.Application;
using AgentSmith.Application.Services;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Infrastructure.Services.Dialogue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Cli;

/// <summary>
/// Builds the DI container for one-shot CLI runs. Two modes:
/// interactive (Console-based dialogue + progress) and spawned-job (Redis-backed
/// when jobId + redisUrl come from a Server-launched container).
/// </summary>
internal static class ServiceProviderFactory
{
    public static ServiceProvider Build(
        bool verbose, bool headless,
        string jobId = "", string redisUrl = "",
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
        services.AddInProcessSandbox();
        services.AddSingleton<Commands.ValidateConceptsCommand>();
        RegisterDialogueAndProgress(services, headless, jobId, redisUrl);

        if (configPath is not null)
            services.AddSingleton(new ServerContext(configPath));

        // Validate the WHOLE graph at build time. The verb handlers resolve their
        // entry services (ExecutePipelineUseCase, …) from this provider; a missing
        // registration anywhere in their dependency chain otherwise stays invisible
        // until a real end-user invocation crashes (e.g. the IActiveRunLease gap
        // that no mock-DI or dry-run test caught). ValidateOnBuild surfaces every
        // unresolvable registration here, at once. ValidateScopes stays off: a
        // one-shot CLI run legitimately resolves from the root provider.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = false,
        });
    }

    private static void RegisterDialogueAndProgress(
        IServiceCollection services, bool headless, string jobId, string redisUrl)
    {
        var spawnedJobMode = !string.IsNullOrWhiteSpace(jobId) && !string.IsNullOrWhiteSpace(redisUrl);
        if (spawnedJobMode)
        {
            var multiplexer = ConnectMultiplexer(redisUrl);
            services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            services.AddSingleton<IMessageBus, RedisMessageBus>();
            services.AddSingleton<IDialogueTransport, RedisDialogueTransport>();
            services.AddSingleton<IProgressReporter>(sp =>
                new RedisProgressReporter(
                    sp.GetRequiredService<IMessageBus>(), jobId,
                    sp.GetRequiredService<ILogger<RedisProgressReporter>>()));
            return;
        }

        services.AddSingleton<IDialogueTransport>(sp =>
            new ConsoleDialogueTransport(
                Console.In, Console.Out,
                sp.GetRequiredService<ILogger<ConsoleDialogueTransport>>()));
        services.AddSingleton<IProgressReporter>(sp =>
            new ConsoleProgressReporter(
                sp.GetRequiredService<ILogger<ConsoleProgressReporter>>(), headless));
    }

    private static IConnectionMultiplexer ConnectMultiplexer(string redisUrl)
    {
        var options = ConfigurationOptions.Parse(redisUrl);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(options);
    }
}
