using System.CommandLine;
using AgentSmith.Cli.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Cli.Commands;

/// <summary>
/// 2026-10-04-2bf2: `init --refresh-principles` is the command line's way to ask for what the
/// dashboard's "Refresh principles" chip asks for — per launch, absent means preserve.
/// </summary>
[Collection("ConsoleOut")]
public sealed class InitCommandOptionTests
{
    private readonly Option<string> _config = new("--config", () => "agentsmith.yml");
    private readonly Option<bool> _verbose = new("--verbose");

    [Fact]
    public void InitCommand_RefreshPrinciplesOption_Parses()
    {
        var command = InitCommand.Create(_config, _verbose);
        var option = (Option<bool>)command.Options.Single(o => o.Name == "refresh-principles");

        var with = command.Parse("--project todolist --refresh-principles");
        var without = command.Parse("--project todolist");

        with.Errors.Should().BeEmpty();
        with.GetValueForOption(option).Should().BeTrue();
        without.GetValueForOption(option).Should().BeFalse("an init that says nothing preserves the file");
    }

    [Fact]
    public async Task InitCommand_RefreshPrinciplesDryRun_SaysItWouldRefresh()
    {
        var (exit, output) = await DryRunAsync("--refresh-principles");

        exit.Should().Be(0);
        output.Should().Contain("Refresh principles: yes");
    }

    [Fact]
    public async Task InitCommand_NoRefreshDryRun_SaysNothingAboutRefresh()
    {
        var (exit, output) = await DryRunAsync();

        exit.Should().Be(0);
        output.Should().NotContain("Refresh principles");
    }

    private async Task<(int Exit, string Output)> DryRunAsync(params string[] extra)
    {
        var root = new RootCommand { InitCommand.Create(_config, _verbose) };
        var original = Console.Out;
        await using var capture = new StringWriter();
        Console.SetOut(capture);
        int exit;
        try
        {
            exit = await root.InvokeAsync(["init", "--project", "todolist", "--dry-run", .. extra]);
        }
        finally
        {
            Console.SetOut(original);
        }

        return (exit, capture.ToString());
    }
}
