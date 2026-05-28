using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0176c: PipelineStepRunner.ComposeStepLabel appends a (repo, component)
/// suffix to the step label when the executing command carries RepoName /
/// ContextName. Lets multi-repo BootstrapRound dispatches render as one
/// row per (repo, component) on the Topology view instead of N identical
/// "Producing bootstrap files" rows.
/// </summary>
public sealed class PipelineStepRunnerStepNameTests
{
    [Fact]
    public void CommandWithRepoNameAndContextName_StepNameCarriesSuffix()
    {
        var cmd = PipelineCommand.SkillRound(
            CommandNames.BootstrapRound, skillName: "project-bootstrap", round: 1,
            repoName: "repo-a", contextName: "api");
        PipelineStepRunner.ComposeStepLabel(cmd).Should().EndWith("(repo-a, api)");
    }

    [Fact]
    public void CommandWithRepoNameOnly_StepNameCarriesRepoSuffix()
    {
        var cmd = PipelineCommand.SkillRound(
            CommandNames.BootstrapRound, skillName: "project-bootstrap", round: 1,
            repoName: "repo-a");
        var label = PipelineStepRunner.ComposeStepLabel(cmd);
        label.Should().EndWith("(repo-a)");
        label.Should().NotContain("(repo-a,");
    }

    [Fact]
    public void CommandWithContextNameOnly_StepNameCarriesContextSuffix()
    {
        var cmd = PipelineCommand.SkillRound(
            CommandNames.BootstrapRound, skillName: "project-bootstrap", round: 1,
            contextName: "api");
        PipelineStepRunner.ComposeStepLabel(cmd).Should().EndWith("(api)");
    }

    [Fact]
    public void CommandWithoutRepoOrContext_StepNameUnchanged()
    {
        var cmd = PipelineCommand.Simple(CommandNames.AnalyzeCode);
        var bare = CommandNames.GetLabel(CommandNames.AnalyzeCode);
        PipelineStepRunner.ComposeStepLabel(cmd).Should().Be(bare);
    }

    [Fact]
    public void MultipleBootstrapRounds_EachCarriesItsOwnRepoComponentSuffix()
    {
        var a = PipelineCommand.SkillRound(CommandNames.BootstrapRound, "project-bootstrap", 1, "repo-a", "api");
        var b = PipelineCommand.SkillRound(CommandNames.BootstrapRound, "project-bootstrap", 1, "repo-a", "client");
        var c = PipelineCommand.SkillRound(CommandNames.BootstrapRound, "project-bootstrap", 1, "repo-b", "default");
        var labelA = PipelineStepRunner.ComposeStepLabel(a);
        var labelB = PipelineStepRunner.ComposeStepLabel(b);
        var labelC = PipelineStepRunner.ComposeStepLabel(c);
        // The base label is shared; the suffix is what distinguishes the rows.
        labelA.Should().NotBe(labelB);
        labelA.Should().NotBe(labelC);
        labelB.Should().NotBe(labelC);
    }
}
