using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Services;
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

        var cmd = new Command("server", "Start as webhook listener + pipeline queue consumer")
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

            var listenerTask = listener.RunAsync(port, cts.Token);
            var consumerTask = StartConsumerAsync(provider, configPath, cts.Token);

            await Task.WhenAll(listenerTask, consumerTask);
        });

        return cmd;
    }

    private static Task StartConsumerAsync(
        IServiceProvider provider, string configPath, CancellationToken ct)
    {
        var queue = provider.GetService<IRedisJobQueue>();
        if (queue is null) return Task.CompletedTask;

        var configLoader = provider.GetRequiredService<IConfigurationLoader>();
        var queueConfig = configLoader.LoadConfig(configPath).Queue;
        var logger = provider.GetRequiredService<ILogger<PipelineQueueConsumer>>();

        var consumer = new PipelineQueueConsumer(
            provider,
            queue,
            configPath,
            queueConfig.MaxParallelJobs,
            queueConfig.ShutdownGraceSeconds,
            logger);

        return consumer.RunAsync(ct);
    }
}
