using System.CommandLine;
using AgentSmith.Cli;
using AgentSmith.Cli.Commands;

Banner.Print();

var configOption = new Option<string>("--config", ConfigDiscovery.Resolve, "Path to configuration file");
var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

var runCommand = RunCommand.Create(configOption, verboseOption);
runCommand.IsHidden = true;

var rootCommand = new RootCommand("Agent Smith — self-hosted AI orchestration")
{
    FixCommand.Create(configOption, verboseOption),
    FeatureCommand.Create(configOption, verboseOption),
    InitCommand.Create(configOption, verboseOption),
    MadCommand.Create(configOption, verboseOption),
    LegalCommand.Create(configOption, verboseOption),
    SecurityScanCommand.Create(configOption, verboseOption),
    ApiScanCommand.Create(configOption, verboseOption),
    SecurityTrendCommand.Create(configOption, verboseOption),
    CompileWikiCommand.Create(configOption, verboseOption),
    AutonomousCommand.Create(configOption, verboseOption),
    SkillsCommand.Create(configOption, verboseOption),
    runCommand,
};

return await rootCommand.InvokeAsync(args);
