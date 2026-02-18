using System.CommandLine;
using AgentSmith.Application;
using AgentSmith.Application.UseCases;
using AgentSmith.Contracts.Services;
using AgentSmith.Host;
using AgentSmith.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var inputArg = new Argument<string>(
    "input", () => "", "Ticket reference and project, e.g. \"fix #123 in payslip\"");

var configOption = new Option<string>(
    "--config", () => "config/agentsmith.yml", "Path to configuration file");

var dryRunOption = new Option<bool>(
    "--dry-run", "Parse intent and show pipeline, but don't execute");

var verboseOption = new Option<bool>(
    "--verbose", "Enable verbose logging");

var headlessOption = new Option<bool>(
    "--headless", "Run without interactive prompts (auto-approve plans)");

var serverOption = new Option<bool>(
    "--server", "Start as webhook listener (HTTP server mode)");

var portOption = new Option<int>(
    "--port", () => 8080, "Port for webhook listener (--server mode)");

var rootCommand = new RootCommand("Agent Smith - AI Coding Agent")
{
    inputArg, configOption, dryRunOption, verboseOption, headlessOption, serverOption, portOption
};

rootCommand.SetHandler(async (string input, string configPath, bool dryRun, bool verbose,
    bool headless, bool server, int port) =>
{
    var provider = BuildServiceProvider(verbose);

    if (server)
    {
        await RunServerMode(provider, configPath, port);
        return;
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.Error.WriteLine("Error: input argument is required (or use --server mode).");
        Environment.ExitCode = 1;
        return;
    }

    if (dryRun)
    {
        await RunDryMode(provider, input, configPath);
        return;
    }

    var useCase = provider.GetRequiredService<ProcessTicketUseCase>();
    var result = await useCase.ExecuteAsync(input, configPath, headless);

    Console.WriteLine(result.Success
        ? $"Success: {result.Message}"
        : $"Failed: {result.Message}");

    Environment.ExitCode = result.Success ? 0 : 1;

}, inputArg, configOption, dryRunOption, verboseOption, headlessOption, serverOption, portOption);

return await rootCommand.InvokeAsync(args);

static ServiceProvider BuildServiceProvider(bool verbose)
{
    var services = new ServiceCollection();
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
    });
    services.AddAgentSmithInfrastructure();
    services.AddAgentSmithCommands();
    return services.BuildServiceProvider();
}

static async Task RunServerMode(ServiceProvider provider, string configPath, int port)
{
    var logger = provider.GetRequiredService<ILogger<WebhookListener>>();
    var listener = new WebhookListener(provider, configPath, logger);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    await listener.RunAsync(port, cts.Token);
}

static async Task RunDryMode(ServiceProvider provider, string input, string configPath)
{
    var configLoader = provider.GetRequiredService<IConfigurationLoader>();
    var intentParser = provider.GetRequiredService<IIntentParser>();

    var config = configLoader.LoadConfig(configPath);
    var intent = await intentParser.ParseAsync(input);

    var projectName = intent.ProjectName.Value;
    if (!config.Projects.TryGetValue(projectName, out var projectConfig))
    {
        Console.Error.WriteLine($"Project '{projectName}' not found in configuration.");
        Environment.ExitCode = 1;
        return;
    }

    if (!config.Pipelines.TryGetValue(projectConfig.Pipeline, out var pipelineConfig))
    {
        Console.Error.WriteLine($"Pipeline '{projectConfig.Pipeline}' not found in configuration.");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Dry run - would execute:");
    Console.WriteLine($"  Project:  {projectName}");
    Console.WriteLine($"  Ticket:   #{intent.TicketId}");
    Console.WriteLine($"  Pipeline: {projectConfig.Pipeline}");
    Console.WriteLine($"  Commands:");
    foreach (var cmd in pipelineConfig.Commands)
        Console.WriteLine($"    - {cmd}");
}
