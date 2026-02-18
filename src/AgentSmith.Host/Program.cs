using System.CommandLine;
using AgentSmith.Application;
using AgentSmith.Application.UseCases;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var inputArg = new Argument<string>(
    "input", "Ticket reference and project, e.g. \"fix #123 in payslip\"");

var configOption = new Option<string>(
    "--config", () => "config/agentsmith.yml", "Path to configuration file");

var dryRunOption = new Option<bool>(
    "--dry-run", "Parse intent and show pipeline, but don't execute");

var verboseOption = new Option<bool>(
    "--verbose", "Enable verbose logging");

var rootCommand = new RootCommand("Agent Smith - AI Coding Agent")
{
    inputArg, configOption, dryRunOption, verboseOption
};

rootCommand.SetHandler(async (string input, string configPath, bool dryRun, bool verbose) =>
{
    var provider = BuildServiceProvider(verbose);

    if (dryRun)
    {
        await RunDryMode(provider, input, configPath);
        return;
    }

    var useCase = provider.GetRequiredService<ProcessTicketUseCase>();
    var result = await useCase.ExecuteAsync(input, configPath);

    Console.WriteLine(result.Success
        ? $"Success: {result.Message}"
        : $"Failed: {result.Message}");

    Environment.ExitCode = result.Success ? 0 : 1;

}, inputArg, configOption, dryRunOption, verboseOption);

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
