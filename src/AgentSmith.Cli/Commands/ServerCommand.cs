using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Commands;

internal static class ServerCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var portOption = new Option<int>("--port", () => 8081, "Port for webhook listener");

        var cmd = new Command("server", "Start as webhook listener (HTTP server mode)")
        {
            portOption, configOption, verboseOption
        };

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var port = ctx.ParseResult.GetValueForOption(portOption);
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);

            var provider = ServiceProviderFactory.Build(verbose, headless: true, string.Empty, string.Empty, configPath);
            var logger = provider.GetRequiredService<ILogger<WebhookListener>>();
            var listener = new WebhookListener(provider, configPath, logger);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await listener.RunAsync(port, cts.Token);
        });

        return cmd;
    }
}
