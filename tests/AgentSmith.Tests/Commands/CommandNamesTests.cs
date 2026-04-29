using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

public class CommandNamesTests
{
    [Fact]
    public void GetLabel_KnownCommand_ReturnsLabel()
    {
        CommandNames.GetLabel(CommandNames.FetchTicket).Should().Be("Fetching ticket");
        CommandNames.GetLabel(CommandNames.Test).Should().Be("Running tests");
        CommandNames.GetLabel(CommandNames.CommitAndPR).Should().Be("Creating pull request");
    }

    [Fact]
    public void GetLabel_UnknownCommand_ReturnsCommandName()
    {
        CommandNames.GetLabel("SomeUnknownCommand").Should().Be("SomeUnknownCommand");
    }

    [Fact]
    public void GetLabel_CaseInsensitive()
    {
        CommandNames.GetLabel("fetchtickeTcommand").Should().Be("Fetching ticket");
    }

    [Fact]
    public void GetLabel_ParameterizedCommand_ReturnsBaseLabel()
    {
        CommandNames.GetLabel("SkillRoundCommand:architect:1").Should().Be("Skill round");
        CommandNames.GetLabel("SwitchSkillCommand:tester").Should().Be("Switching skill");
    }

    [Fact]
    public void AllConstants_HaveLabels()
    {
        var constants = new[]
        {
            CommandNames.FetchTicket, CommandNames.CheckoutSource,
            CommandNames.BootstrapProject, CommandNames.LoadCodeMap,
            CommandNames.LoadCodingPrinciples,
            CommandNames.LoadContext,
            CommandNames.AnalyzeCode, CommandNames.GeneratePlan,
            CommandNames.Approval, CommandNames.AgenticExecute,
            CommandNames.Test, CommandNames.WriteRunResult,
            CommandNames.CommitAndPR, CommandNames.InitCommit,
            CommandNames.GenerateTests, CommandNames.GenerateDocs,
            CommandNames.Triage, CommandNames.SwitchSkill,
            CommandNames.SkillRound, CommandNames.ConvergenceCheck,
        };

        foreach (var name in constants)
        {
            var label = CommandNames.GetLabel(name);
            label.Should().NotBe(name, $"Command '{name}' should have a label different from its name");
        }
    }
}
