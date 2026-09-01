using System.CommandLine;
using AgentSmith.Cli.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Cli.Commands;

/// <summary>
/// 2026-09-01-a17c: `--ticket` carries the identifier its tracker uses. A Jira key was
/// refused at parse time while the option was an int, so no Jira-tracked project could be
/// started from the command line at all.
/// </summary>
[Collection("ConsoleOut")]
public sealed class TicketOptionTests
{
    [Fact]
    public async Task Cli_AJiraKey_IsAccepted()
    {
        var (exit, output) = await DryRunAsync("code", "SCRUM-8");

        exit.Should().Be(0);
        output.Should().Contain("SCRUM-8", "the key reaches the request as the tracker writes it");
    }

    [Fact]
    public async Task Cli_ANumericId_StillWorks()
    {
        var (exit, output) = await DryRunAsync("code", "54");

        exit.Should().Be(0);
        output.Should().Contain("54");
    }

    [Theory]
    [InlineData("fix", "SCRUM-8")]
    [InlineData("feature", "SCRUM-8")]
    [InlineData("mad", "PROJ-1234")]
    public async Task Cli_EveryVerbTakingATicket_AcceptsATrackerKey(string verb, string ticket)
    {
        var (exit, output) = await DryRunAsync(verb, ticket);

        exit.Should().Be(0);
        output.Should().Contain(ticket);
    }

    private static async Task<(int Exit, string Output)> DryRunAsync(string verb, string ticket)
    {
        var configOption = new Option<string>("--config", () => "agentsmith.yml", "Path to configuration file");
        var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");
        var root = new RootCommand
        {
            CodeCommand.Create(configOption, verboseOption),
            CodeCommand.CreateAlias("fix", "Deprecated alias for 'code'", configOption, verboseOption),
            CodeCommand.CreateAlias("feature", "Deprecated alias for 'code'", configOption, verboseOption),
            MadCommand.Create(configOption, verboseOption),
        };

        var original = Console.Out;
        await using var capture = new StringWriter();
        Console.SetOut(capture);
        int exit;
        try
        {
            exit = await root.InvokeAsync([verb, "--ticket", ticket, "--project", "todolist", "--dry-run"]);
        }
        finally
        {
            Console.SetOut(original);
        }

        return (exit, capture.ToString());
    }
}
