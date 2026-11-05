using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// The CLI's verb tree. Every verb listed here runs a pipeline this product offers, or manages
/// the product itself; a verb that could only fail would mislead in --help.
/// </summary>
internal static class CliRootCommand
{
    private const string ConfigOptionName = "config";

    /// <summary>2026-11-02-2b26a: the parser Program runs. A configuration problem is the operator's to fix and
    /// prints as its message; anything else keeps the full exception, which is a defect report.</summary>
    public static Parser BuildParser() =>
        new CommandLineBuilder(Create()).UseDefaults().UseExceptionHandler(OnException).Build();

    private static void OnException(Exception exception, InvocationContext context)
    {
        context.ExitCode = 1;
        if (exception is not ConfigurationException config)
        {
            Console.Error.WriteLine($"Unhandled exception: {exception}");
            return;
        }
        Console.Error.WriteLine(config.Message);
        if (ConfigPath(context.ParseResult) is { } path && !File.Exists(path))
            Console.Error.WriteLine($"Looked in: {string.Join(", ", ConfigDiscovery.Paths)}");
    }

    private static string? ConfigPath(ParseResult parse) =>
        parse.CommandResult.Children.OfType<OptionResult>()
            .FirstOrDefault(option => option.Option.Name == ConfigOptionName)?.GetValueOrDefault<string>();

    public static RootCommand Create()
    {
        var configOption = new Option<string>("--" + ConfigOptionName, ConfigDiscovery.Resolve, "Path to configuration file");
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
