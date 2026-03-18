using System.CommandLine;
using AgentSmith.Infrastructure.Models;
using System.CommandLine.Invocation;

using AgentSmith.Application;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Host.Services;
using AgentSmith.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

PrintBanner();

// --- Shared options ---

var configOption = new Option<string>(
    "--config", () => "config/agentsmith.yml", "Path to configuration file");

var verboseOption = new Option<bool>(
    "--verbose", "Enable verbose logging");

var headlessOption = new Option<bool>(
    "--headless", "Run without interactive prompts (auto-approve plans)");

var jobIdOption = new Option<string>(
    "--job-id", () => string.Empty, "Redis Streams job ID (K8s job mode)");

var redisUrlOption = new Option<string>(
    "--redis-url", () => string.Empty, "Redis connection URL for K8s job mode");

var channelIdOption = new Option<string>(
    "--channel-id", () => string.Empty, "Source channel ID (for context in K8s job mode)");

var platformOption = new Option<string>(
    "--platform", () => string.Empty, "Source platform: slack|teams|whatsapp");

// --- 'run' subcommand (replaces old root command) ---

var runInputArg = new Argument<string>(
    "input", "Ticket reference and project, e.g. \"fix #123 in my-project\"");

var dryRunOption = new Option<bool>(
    "--dry-run", "Parse intent and show pipeline, but don't execute");

var pipelineOption = new Option<string>(
    "--pipeline", () => string.Empty, "Override pipeline name (e.g. init-project)");

var runCommand = new Command("run", "Execute a pipeline (ticket, analysis, scan)")
{
    runInputArg, configOption, dryRunOption, verboseOption, headlessOption,
    jobIdOption, redisUrlOption, channelIdOption, platformOption, pipelineOption
};

runCommand.SetHandler(async (InvocationContext ctx) =>
{
    var input = ctx.ParseResult.GetValueForArgument(runInputArg);
    var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
    var dryRun = ctx.ParseResult.GetValueForOption(dryRunOption);
    var verbose = ctx.ParseResult.GetValueForOption(verboseOption);
    var headless = ctx.ParseResult.GetValueForOption(headlessOption);
    var jobId = ctx.ParseResult.GetValueForOption(jobIdOption) ?? string.Empty;
    var redisUrl = ctx.ParseResult.GetValueForOption(redisUrlOption) ?? string.Empty;
    var pipelineOverride = ctx.ParseResult.GetValueForOption(pipelineOption) ?? string.Empty;

    var provider = BuildServiceProvider(verbose, headless, jobId, redisUrl);

    if (dryRun)
    {
        await RunDryMode(provider, input, configPath, pipelineOverride);
        return;
    }

    var useCase = provider.GetRequiredService<ExecutePipelineUseCase>();
    var pipeline = string.IsNullOrWhiteSpace(pipelineOverride) ? null : pipelineOverride;

    CommandResult result;
    try
    {
        result = await useCase.ExecuteAsync(input, configPath, headless, pipeline, CancellationToken.None);
    }
    catch (Exception ex)
    {
        result = CommandResult.Fail($"Unhandled exception: {ex.Message}");
        Console.Error.WriteLine($"Fatal: {ex}");
    }

    if (!string.IsNullOrWhiteSpace(jobId))
    {
        var reporter = provider.GetRequiredService<IProgressReporter>();
        if (result.IsSuccess)
            await reporter.ReportDoneAsync(result.Message, result.PrUrl, CancellationToken.None);
        else
            await reporter.ReportErrorAsync(
                result.Message, result.FailedStep, result.TotalSteps, result.StepName, CancellationToken.None);
    }

    Console.WriteLine(result.IsSuccess
        ? $"Success: {result.Message}"
        : $"Failed: {result.Message}");

    ctx.ExitCode = result.IsSuccess ? 0 : 1;
});

// --- 'security-scan' subcommand ---

var repoOption = new Option<string>(
    "--repo", "Path or URL of repository to scan") { IsRequired = true };

var prOption = new Option<string>(
    "--pr", () => string.Empty, "PR/MR number (diff only; if absent, full repo scan)");

var outputOption = new Option<string>(
    "--output", () => "console", "Output format: sarif | markdown | console");

var projectOption = new Option<string>(
    "--project", () => string.Empty, "Project name from config (for multi-project configs)");

var securityScanCommand = new Command("security-scan", "Analyze code for security vulnerabilities")
{
    repoOption, prOption, outputOption, projectOption, configOption, verboseOption
};

securityScanCommand.SetHandler(async (InvocationContext ctx) =>
{
    var repo = ctx.ParseResult.GetValueForOption(repoOption)!;
    var pr = ctx.ParseResult.GetValueForOption(prOption) ?? string.Empty;
    var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "console";
    var project = ctx.ParseResult.GetValueForOption(projectOption) ?? string.Empty;
    var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
    var verbose = ctx.ParseResult.GetValueForOption(verboseOption);

    var provider = BuildServiceProvider(verbose, headless: true, jobId: string.Empty, redisUrl: string.Empty);
    var useCase = provider.GetRequiredService<ExecutePipelineUseCase>();

    // Build input string for the security-scan pipeline
    var input = !string.IsNullOrWhiteSpace(project)
        ? $"security-scan in {project}"
        : $"security-scan in {Path.GetFileName(Path.GetFullPath(repo))}";

    // TODO: pass --repo and --pr to pipeline context once ExecutePipelineUseCase
    // supports PipelineContext injection (currently input string is the only entry point)

    CommandResult result;
    try
    {
        result = await useCase.ExecuteAsync(input, configPath, headless: true, "security-scan", CancellationToken.None);
    }
    catch (Exception ex)
    {
        result = CommandResult.Fail($"Unhandled exception: {ex.Message}");
        Console.Error.WriteLine($"Fatal: {ex}");
    }

    Console.WriteLine(result.IsSuccess
        ? $"Security scan complete: {result.Message}"
        : $"Security scan failed: {result.Message}");

    ctx.ExitCode = result.IsSuccess ? 0 : 1;
});

// --- 'server' subcommand ---

var portOption = new Option<int>(
    "--port", () => 8081, "Port for webhook listener");

var serverCommand = new Command("server", "Start as webhook listener (HTTP server mode)")
{
    portOption, configOption, verboseOption
};

serverCommand.SetHandler(async (InvocationContext ctx) =>
{
    var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
    var port = ctx.ParseResult.GetValueForOption(portOption);
    var verbose = ctx.ParseResult.GetValueForOption(verboseOption);

    var provider = BuildServiceProvider(verbose, headless: true, jobId: string.Empty, redisUrl: string.Empty);
    await RunServerMode(provider, configPath, port);
});

// --- Root command ---

var rootCommand = new RootCommand("Agent Smith — self-hosted AI orchestration")
{
    runCommand,
    securityScanCommand,
    serverCommand,
};

return await rootCommand.InvokeAsync(args);

// --- Helper methods ---

static ServiceProvider BuildServiceProvider(
    bool verbose, bool headless, string jobId, string redisUrl)
{
    var services = new ServiceCollection();
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
    });
    services.AddAgentSmithInfrastructure();
    services.AddAgentSmithCommands();
    RegisterWebhookHandlers(services);
    RegisterProgressReporter(services, headless, jobId, redisUrl);
    return services.BuildServiceProvider();
}

static void RegisterWebhookHandlers(IServiceCollection services)
{
    services.AddSingleton<IWebhookHandler, AgentSmith.Host.Services.Webhooks.GitHubIssueWebhookHandler>();
    services.AddSingleton<IWebhookHandler, AgentSmith.Host.Services.Webhooks.GitHubPrLabelWebhookHandler>();
    services.AddSingleton<IWebhookHandler, AgentSmith.Host.Services.Webhooks.GitLabMrLabelWebhookHandler>();
    services.AddSingleton<IWebhookHandler, AgentSmith.Host.Services.Webhooks.AzureDevOpsWorkItemWebhookHandler>();
}

static void RegisterProgressReporter(
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
        {
            var logger = sp.GetRequiredService<ILogger<ConsoleProgressReporter>>();
            return new ConsoleProgressReporter(logger, headless);
        });
    }
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
    Console.WriteLine("  Your AI. Your infrastructure. Your rules.\n");
    Console.WriteLine("  code · legal · security · workflows\n");
    Console.ForegroundColor = original;
}

static async Task RunDryMode(ServiceProvider provider, string input, string configPath, string pipelineOverride)
{
    var configLoader = provider.GetRequiredService<IConfigurationLoader>();
    var intentParser = provider.GetRequiredService<IIntentParser>();

    var config = configLoader.LoadConfig(configPath);
    var intent = await intentParser.ParseAsync(input, CancellationToken.None);

    var projectName = intent.ProjectName.Value;
    if (!config.Projects.TryGetValue(projectName, out var projectConfig))
    {
        Console.Error.WriteLine($"Project '{projectName}' not found in configuration.");
        Environment.ExitCode = 1;
        return;
    }

    var pipelineName = string.IsNullOrWhiteSpace(pipelineOverride) ? projectConfig.Pipeline : pipelineOverride;
    var commands = PipelinePresets.TryResolve(pipelineName);
    if (commands is null)
    {
        Console.Error.WriteLine($"Pipeline '{projectConfig.Pipeline}' not found in presets.");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Dry run - would execute:");
    Console.WriteLine($"  Project:  {projectName}");
    Console.WriteLine($"  Ticket:   #{intent.TicketId}");
    Console.WriteLine($"  Pipeline: {pipelineName}");
    Console.WriteLine($"  Commands:");
    foreach (var cmd in commands)
        Console.WriteLine($"    - {cmd}");
}
