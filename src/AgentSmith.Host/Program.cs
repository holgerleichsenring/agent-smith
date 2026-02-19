using System.CommandLine;
using System.CommandLine.Invocation;

using AgentSmith.Application;
using AgentSmith.Application.Services;
using AgentSmith.Application.UseCases;
using AgentSmith.Contracts.Services;
using AgentSmith.Host;
using AgentSmith.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

PrintBanner();

var inputArg = new Argument<string>(
    "input", () => "", "Ticket reference and project, e.g. \"fix #123 in my-project\"");

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

var jobIdOption = new Option<string>(
    "--job-id", () => string.Empty, "Redis Streams job ID (K8s job mode)");

var redisUrlOption = new Option<string>(
    "--redis-url", () => string.Empty, "Redis connection URL for K8s job mode");

var channelIdOption = new Option<string>(
    "--channel-id", () => string.Empty, "Source channel ID (for context in K8s job mode)");

var platformOption = new Option<string>(
    "--platform", () => string.Empty, "Source platform: slack|teams|whatsapp");

var rootCommand = new RootCommand("Agent Smith - AI Coding Agent")
{
    inputArg, configOption, dryRunOption, verboseOption, headlessOption,
    serverOption, portOption, jobIdOption, redisUrlOption, channelIdOption, platformOption
};

rootCommand.SetHandler(async (InvocationContext ctx) =>
{
    var input = ctx.ParseResult.GetValueForArgument(inputArg);
    var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
    var dryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
    var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
    var headless = ctx.ParseResult.GetValueForOption(headlessOption);
    var server = ctx.ParseResult.GetValueForOption(serverOption);
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var jobId = ctx.ParseResult.GetValueForOption(jobIdOption) ?? string.Empty;
    var redisUrl = ctx.ParseResult.GetValueForOption(redisUrlOption) ?? string.Empty;

    var provider = BuildServiceProvider(verbose, headless, jobId, redisUrl);

    if (server)
    {
        await RunServerMode(provider, configPath, port);
        return;
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        Console.Error.WriteLine("Error: input argument is required (or use --server mode).");
        ctx.ExitCode = 1;
        return;
    }

    if (dryRun)
    {
        await RunDryMode(provider, input, configPath);
        return;
    }

    var useCase = provider.GetRequiredService<ProcessTicketUseCase>();
    var result = await useCase.ExecuteAsync(input, configPath, headless);

    // In K8s job mode, signal done/error via the progress reporter (Redis)
    if (!string.IsNullOrWhiteSpace(jobId))
    {
        var reporter = provider.GetRequiredService<IProgressReporter>();
        if (result.Success)
            await reporter.ReportDoneAsync(result.Message);
        else
            await reporter.ReportErrorAsync(result.Message);
    }

    Console.WriteLine(result.Success
        ? $"Success: {result.Message}"
        : $"Failed: {result.Message}");

    ctx.ExitCode = result.Success ? 0 : 1;
});

return await rootCommand.InvokeAsync(args);

static ServiceProvider BuildServiceProvider(
    bool verbose, bool headless = false, string jobId = "", string redisUrl = "")
{
    var services = new ServiceCollection();
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
    });
    services.AddAgentSmithInfrastructure();
    services.AddAgentSmithCommands();
    RegisterProgressReporter(services, headless, jobId, redisUrl);
    return services.BuildServiceProvider();
}

static void RegisterProgressReporter(
    IServiceCollection services, bool headless, string jobId, string redisUrl)
{
    services.AddSingleton<IProgressReporter>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<ConsoleProgressReporter>>();
        // In K8s job mode we force headless=true on the console reporter
        // (the RedisProgressReporter in the Dispatcher handles interactive questions).
        var forceHeadless = headless || !string.IsNullOrWhiteSpace(jobId);
        return new ConsoleProgressReporter(logger, forceHeadless);
    });
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

static void PrintBanner()
{
    var original = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine(@"
  █████╗  ██████╗ ███████╗███╗   ██╗████████╗    ███████╗███╗   ███╗██╗████████╗██╗  ██╗
 ██╔══██╗██╔════╝ ██╔════╝████╗  ██║╚══██╔══╝    ██╔════╝████╗ ████║██║╚══██╔══╝██║  ██║
 ███████║██║  ███╗█████╗  ██╔██╗ ██║   ██║       ███████╗██╔████╔██║██║   ██║   ███████║
 ██╔══██║██║   ██║██╔══╝  ██║╚██╗██║   ██║       ╚════██║██║╚██╔╝██║██║   ██║   ██╔══██║
 ██║  ██║╚██████╔╝███████╗██║ ╚████║   ██║       ███████║██║ ╚═╝ ██║██║   ██║   ██║  ██║
 ╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝  ╚═══╝  ╚═╝       ╚══════╝╚═╝     ╚═╝╚═╝   ╚═╝   ╚═╝  ╚═╝");
    Console.ForegroundColor = ConsoleColor.DarkGreen;
    Console.WriteLine("  Self-hosted AI coding agent · ticket → code → PR\n");

    Console.ForegroundColor = original;
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
