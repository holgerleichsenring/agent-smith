using AgentSmith.Application.Services.Tools;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// 2026-11-18-4f3e: the pinned coding master leads a sub-agent that builds the spec and writes the plan into the run
/// record directory. 2026-11-18-98fa: 3.1.1 (skills v5.37.1) reports in markdown and ends with finish_spec.
/// </summary>
public sealed class CodingMasterLeadPromptTests
{
    private static string Folded() => PackagedMaster.Folded("coding-agent-master");

    [Fact]
    public void PackagedCodingMaster_LeadsASubAgentThatBuilds()
    {
        var master = Folded();

        master.Should().Contain("A sub-agent builds it; you know the goal, you did not write the code");
        master.Should().Contain("A child you start with `spawn_agents` and `background: true` works beside you");
        master.Should().Contain("Something missing or wrong goes back to the child with `send_message`");
        master.Should().Contain("Its answer is its account; you decide by looking");
    }

    [Fact]
    public void PackagedCodingMaster_WritesThePlanIntoTheRunRecordDirectory() =>
        Folded().Should().Contain("Write `<repo>/{RunRecordDir}/plan.md` beside it",
            "WriteRunResultHandler shows plan.md from the run record directory as the plan");

    [Fact]
    public void PackagedCodingMaster_DeclaresNoFixRoundCounter()
    {
        var master = PackagedMaster.Read("coding-agent-master");

        master.Should().NotContain("MaxFixIterations", "3.0.0 keeps going while rounds make progress");
        master.Should().Contain("## Ticket instructions", "the heading the skills repository's validate-skills.sh requires");
        Folded().Should().Contain("when they stop making progress, report the red honestly with the failing output");
    }

    /// <summary>2026-11-18-98fa: the master's report is markdown that nothing parses, and every argument the master is
    /// told to pass to finish_spec is one the tool declares — the contract the skill and the backend share.</summary>
    [Fact]
    public void PackagedCodingMaster_ReportsInMarkdown_ThenCallsFinishSpecWithItsDeclaredArguments()
    {
        var master = Folded();
        master.Should().Contain("Your report is your last message, written in markdown").And.Contain("Nothing parses it");
        master.Should().NotContain("```verdict", "the fenced verdict block is gone");

        var schema = new FinishSpecToolHost().GetTools(null, null).Single().JsonSchema.ToString();
        foreach (var argument in new[] { "status", "summary", "failing_tests", "baseline_failing_tests", "follow_ups", "blocker", "ignored_instructions" })
        {
            master.Should().Contain($"`{argument}`", "the skill names it");
            schema.Should().Contain($"\"{argument}\"", "finish_spec declares it");
        }
        foreach (var status in new[] { "green", "no-tests", "failed", "blocked" })
            master.Should().Contain($"`{status}`");
    }
}
