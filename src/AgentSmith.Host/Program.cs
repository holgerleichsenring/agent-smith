using System.CommandLine;
using AgentSmith.Host;
using AgentSmith.Host.Commands;

Banner.Print();

var configOption = new Option<string>("--config", () => "config/agentsmith.yml", "Path to configuration file");
var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

var rootCommand = new RootCommand("Agent Smith — self-hosted AI orchestration")
{
    RunCommand.Create(configOption, verboseOption),
    SecurityScanCommand.Create(configOption, verboseOption),
    ApiScanCommand.Create(configOption, verboseOption),
    ServerCommand.Create(configOption, verboseOption),
};

return await rootCommand.InvokeAsync(args);
