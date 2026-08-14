using AgentSmith.Application;
using AgentSmith.Application.Services;
using AgentSmith.Cli.Services;
using AgentSmith.Cli.Services.Demo;
using AgentSmith.Cli.Services.Preflight;
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
/// Builds the DI container for one-shot CLI runs — interactive (console dialogue and
/// progress) or spawned-job (Redis, when jobId + redisUrl come from the server).
/// </summary>
internal static class ServiceProviderFactory
{
    /// <summary>p0419: configPath comes FIRST and has no default, so no verb can build a
    /// container without deciding what config it runs on — as a trailing optional
    /// parameter it was simply never passed by `run`. See CliConfigCompositionTests.</summary>
    public static ServiceProvider Build(
        string configPath, bool verbose, bool headless,
        string jobId = "", string redisUrl = "")
    {
        return BuildProvider(BuildServices(verbose, headless, jobId, redisUrl, configPath));
    }

    /// <summary>p0324: the CLI graph plus the doctor verb's preflight and probe seams.</summary>
    public static ServiceProvider BuildDoctor(bool verbose, string configPath)
    {
        var services = BuildServices(verbose, headless: true, jobId: "", redisUrl: "", configPath);
        services.AddDoctorPreflight();
        return BuildProvider(services);
    }

    /// <summary>p0326: the CLI graph plus the demo preflight, materializer and runner.</summary>
    public static ServiceProvider BuildDemo(bool verbose, string configPath)
    {
        var services = BuildServices(verbose, headless: true, jobId: "", redisUrl: "", configPath);
        services.AddDemo();
        return BuildProvider(services);
    }

    private static ServiceCollection BuildServices(
        bool verbose, bool headless, string jobId, string redisUrl, string configPath)
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
        // p0391b: `agentsmith config validate` runs the server's own rules over a file.
        services.AddSingleton<ConfigValidator>();
        RegisterDialogueAndProgress(services, headless, jobId, redisUrl);

        if (!string.IsNullOrWhiteSpace(configPath))
        {
            services.AddSingleton(new ServerContext(configPath));
            // p0417: the core chain registers AgentSmithConfig.Empty() and the server
            // overrides it; the CLI did not, so eight handlers ran on "nothing is
            // configured" — run 0adc lost its feed credentials. Last binding wins.
            services.AddSingleton<AgentSmithConfig>(sp =>
                sp.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath));
        }

        return services;
    }

    // Validate the WHOLE graph at build time: a missing registration deep in a verb's
    // dependency chain otherwise stays invisible until an end-user invocation crashes
    // (the IActiveRunLease gap no mock-DI test caught). ValidateScopes stays off — a
    // one-shot CLI run legitimately resolves from the root provider.
    private static ServiceProvider BuildProvider(ServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = false,
        });

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
