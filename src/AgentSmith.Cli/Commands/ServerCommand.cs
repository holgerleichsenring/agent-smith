using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
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

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var port = ctx.ParseResult.GetValueForOption(portOption);
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);

            var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty, configPath);
            var listenerLogger = provider.GetRequiredService<ILogger<WebhookListener>>();
            var listener = new WebhookListener(provider, configPath, listenerLogger);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await Task.WhenAll(
                listener.RunAsync(port, cts.Token),
                StartConsumerAsync(provider, configPath, cts.Token),
                StartHousekeepingLeaderAsync(provider, configPath, cts.Token),
                StartPollerLeaderAsync(provider, configPath, cts.Token));
        });

        return cmd;
    }

    private static Task StartConsumerAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var queue = provider.GetService<IRedisJobQueue>();
        if (queue is null) return Task.CompletedTask;

        var queueConfig = provider.GetRequiredService<IConfigurationLoader>()
            .LoadConfig(configPath).Queue;
        return new PipelineQueueConsumer(
            provider, queue, configPath,
            queueConfig.MaxParallelJobs, queueConfig.ShutdownGraceSeconds,
            provider.GetRequiredService<ILogger<PipelineQueueConsumer>>())
            .RunAsync(ct);
    }

    private static Task StartHousekeepingLeaderAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var lease = provider.GetService<IRedisLeaderLease>();
        if (lease is null) return Task.CompletedTask;

        var leader = new LeaderElectedHostedService(
            "agentsmith:leader:housekeeping",
            leaderCt => RunHousekeepingAsync(provider, configPath, leaderCt),
            lease,
            provider.GetRequiredService<ILogger<LeaderElectedHostedService>>());
        return leader.RunAsync(ct);
    }

    private static Task RunHousekeepingAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var heartbeat = provider.GetRequiredService<IJobHeartbeatService>();
        var queue = provider.GetRequiredService<IRedisJobQueue>();
        var ticketFactory = provider.GetRequiredService<ITicketProviderFactory>();
        var transitionerFactory = provider.GetRequiredService<ITicketStatusTransitionerFactory>();
        var configLoader = provider.GetRequiredService<IConfigurationLoader>();

        var stale = new StaleJobDetector(
            heartbeat, ticketFactory, transitionerFactory, configLoader, configPath,
            provider.GetRequiredService<ILogger<StaleJobDetector>>());
        var reconciler = new EnqueuedReconciler(
            heartbeat, queue, ticketFactory, configLoader, configPath,
            provider.GetRequiredService<ILogger<EnqueuedReconciler>>());
        return Task.WhenAll(stale.RunAsync(ct), reconciler.RunAsync(ct));
    }

    private static Task StartPollerLeaderAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var lease = provider.GetService<IRedisLeaderLease>();
        if (lease is null) return Task.CompletedTask;

        var leader = new LeaderElectedHostedService(
            "agentsmith:leader:poller",
            leaderCt => RunPollerHostAsync(provider, configPath, leaderCt),
            lease,
            provider.GetRequiredService<ILogger<LeaderElectedHostedService>>());
        return leader.RunAsync(ct);
    }

    private static Task RunPollerHostAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var config = provider.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath);
        var pollers = BuildPollers(provider, config);
        var host = new PollerHostedService(
            pollers,
            provider.GetRequiredService<ITicketClaimService>(),
            provider.GetRequiredService<IConfigurationLoader>(),
            configPath,
            provider.GetRequiredService<ILogger<PollerHostedService>>());
        return host.RunAsync(ct);
    }

    private static IEnumerable<IEventPoller> BuildPollers(
        IServiceProvider provider, AgentSmithConfig config)
    {
        var ticketFactory = provider.GetRequiredService<ITicketProviderFactory>();
        var transitionerFactory = provider.GetRequiredService<ITicketStatusTransitionerFactory>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        foreach (var (name, project) in config.Projects)
        {
            if (!project.Polling.Enabled) continue;
            if (project.Tickets.Type.Equals("github", StringComparison.OrdinalIgnoreCase))
                yield return new GitHubIssuePoller(
                    name, project, ticketFactory,
                    transitionerFactory.Create(project.Tickets),
                    loggerFactory.CreateLogger<GitHubIssuePoller>());
        }
    }
}
