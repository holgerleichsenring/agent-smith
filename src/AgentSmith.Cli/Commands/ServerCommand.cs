using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Cli.Services;
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
                StartStaleDetectorAsync(provider, configPath, cts.Token),
                StartReconcilerAsync(provider, configPath, cts.Token));
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

    private static Task StartStaleDetectorAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var heartbeat = provider.GetService<IJobHeartbeatService>();
        if (heartbeat is null) return Task.CompletedTask;

        return new StaleJobDetector(
            heartbeat,
            provider.GetRequiredService<ITicketProviderFactory>(),
            provider.GetRequiredService<ITicketStatusTransitionerFactory>(),
            provider.GetRequiredService<IConfigurationLoader>(),
            configPath,
            provider.GetRequiredService<ILogger<StaleJobDetector>>())
            .RunAsync(ct);
    }

    private static Task StartReconcilerAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var heartbeat = provider.GetService<IJobHeartbeatService>();
        var queue = provider.GetService<IRedisJobQueue>();
        if (heartbeat is null || queue is null) return Task.CompletedTask;

        return new EnqueuedReconciler(
            heartbeat, queue,
            provider.GetRequiredService<ITicketProviderFactory>(),
            provider.GetRequiredService<IConfigurationLoader>(),
            configPath,
            provider.GetRequiredService<ILogger<EnqueuedReconciler>>())
            .RunAsync(ct);
    }
}
