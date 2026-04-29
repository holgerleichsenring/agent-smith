using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Health;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Commands;

internal static class ServerCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var portOption = new Option<int>("--port", () => 8081, "Port for webhook listener");
        var cmd = new Command(
            "server",
            "Start webhook listener + pipeline queue consumer + lifecycle housekeeping")
        {
            portOption, configOption, verboseOption
        };
        cmd.SetHandler(async (InvocationContext ctx) => await RunAsync(
            ctx.ParseResult.GetValueForOption(configOption)!,
            ctx.ParseResult.GetValueForOption(portOption),
            ctx.ParseResult.GetValueForOption(verboseOption)));
        return cmd;
    }

    private static async Task RunAsync(string configPath, int port, bool verbose)
    {
        var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty, configPath);
        var (webhook, queue, housekeeping, poller) = (
            new SubsystemHealth("webhook"), new SubsystemHealth("queue_consumer"),
            new SubsystemHealth("housekeeping"), new SubsystemHealth("poller"));
        var allHealths = CollectHealths(provider, webhook, queue, housekeeping, poller);

        var listener = new WebhookListener(provider, configPath, webhook, allHealths,
            provider.GetRequiredService<ILogger<WebhookListener>>());
        var retry = provider.GetRequiredService<IConfigurationLoader>()
            .LoadConfig(configPath).Queue.RedisRetryIntervalSeconds;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        await Task.WhenAll(
            listener.RunAsync(port, cts.Token),
            StartConsumerAsync(provider, configPath, queue, retry, cts.Token),
            LeaderSubsystemRunner.RunAsync(provider, housekeeping, "agentsmith:leader:housekeeping",
                ct => RunHousekeepingAsync(provider, configPath, ct), retry, cts.Token),
            LeaderSubsystemRunner.RunAsync(provider, poller, "agentsmith:leader:poller",
                ct => RunPollerHostAsync(provider, configPath, ct), retry, cts.Token));
    }

    private static IReadOnlyList<ISubsystemHealth> CollectHealths(
        IServiceProvider provider, params ISubsystemHealth[] subsystems)
    {
        var redis = provider.GetServices<ISubsystemHealth>()
            .FirstOrDefault(h => h.Name == "redis");
        return redis is null ? subsystems : subsystems.Append(redis).ToArray();
    }

    private static Task StartConsumerAsync(
        IServiceProvider provider, string configPath, SubsystemHealth health,
        int retryIntervalSeconds, CancellationToken ct)
    {
        var queueConfig = provider.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath).Queue;
        return SubsystemTask.RunRedisGatedAsync<IRedisJobQueue>(
            provider, health, retryIntervalSeconds,
            (queue, t) => new PipelineQueueConsumer(
                provider, queue, configPath,
                queueConfig.MaxParallelJobs, queueConfig.ShutdownGraceSeconds,
                provider.GetRequiredService<ILogger<PipelineQueueConsumer>>())
                .RunAsync(t),
            provider.GetRequiredService<ILogger<PipelineQueueConsumer>>(), ct);
    }

    private static Task RunHousekeepingAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var heartbeat = provider.GetRequiredService<IJobHeartbeatService>();
        var queue = provider.GetRequiredService<IRedisJobQueue>();
        var ticketFactory = provider.GetRequiredService<Contracts.Providers.ITicketProviderFactory>();
        var transitionerFactory = provider.GetRequiredService<ITicketStatusTransitionerFactory>();
        var configLoader = provider.GetRequiredService<IConfigurationLoader>();
        var stale = new StaleJobDetector(
            heartbeat, ticketFactory, transitionerFactory, configLoader, configPath,
            provider.GetRequiredService<ILogger<StaleJobDetector>>());
        var reconciler = new EnqueuedReconciler(
            heartbeat, queue, ticketFactory, configLoader,
            provider.GetRequiredService<IPipelineConfigResolver>(), configPath,
            provider.GetRequiredService<ILogger<EnqueuedReconciler>>());
        return Task.WhenAll(stale.RunAsync(ct), reconciler.RunAsync(ct));
    }

    private static Task RunPollerHostAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var config = provider.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath);
        var host = new PollerHostedService(
            PollerFactory.Build(provider, config),
            provider.GetRequiredService<ITicketClaimService>(),
            provider.GetRequiredService<IConfigurationLoader>(),
            configPath,
            provider.GetRequiredService<ILogger<PollerHostedService>>());
        return host.RunAsync(ct);
    }
}
