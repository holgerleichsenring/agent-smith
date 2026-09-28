using System.CommandLine;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// The CLI's verb tree. Every verb listed here runs a pipeline this product offers, or manages
/// the product itself; a verb that could only fail would mislead in --help.
/// </summary>
internal static class CliRootCommand
{
    public static RootCommand Create()
    {
        var configOption = new Option<string>("--config", ConfigDiscovery.Resolve, "Path to configuration file");
        var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

        var runCommand = RunCommand.Create(configOption, verboseOption);
        runCommand.IsHidden = true;

        return new RootCommand("Agent Smith — self-hosted AI orchestration")
        {
            DoctorCommand.Create(configOption, verboseOption),
            DemoCommand.Create(configOption, verboseOption),
            CodeCommand.Create(configOption, verboseOption),
            // Retired verbs kept as aliases — every existing script keeps working and
            // is told once where the verb went.
            CodeCommand.CreateAlias("fix", "Deprecated alias for 'code'", configOption, verboseOption),
            CodeCommand.CreateAlias("feature", "Deprecated alias for 'code'", configOption, verboseOption),
            InitCommand.Create(configOption, verboseOption),
            MadCommand.Create(configOption, verboseOption),
            LegalCommand.Create(configOption, verboseOption),
            SecurityScanCommand.Create(configOption, verboseOption),
            ApiScanCommand.Create(configOption, verboseOption),
            SecurityTrendCommand.Create(configOption, verboseOption),
            CompileWikiCommand.Create(configOption, verboseOption),
            SkillsCommand.Create(configOption, verboseOption),
            ValidateConceptsCommand.Create(configOption, verboseOption),
            DatabaseCommand.Create(configOption, verboseOption),
            ConfigCommand.Create(configOption, verboseOption),
            ArchiveCommand.Create(configOption, verboseOption),
            runCommand,
        };
    }
}
