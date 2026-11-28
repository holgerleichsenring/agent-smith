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

        // 2026-11-19-55ec: a fresh sub-agent reviews; the master decides and is not the reviewer.
        master.Should().Contain("A sub-agent builds it, a fresh\nsub-agent reviews it, and you decide".Replace("\n", " "));
        master.Should().Contain("A child you start with `spawn_agents` and `background: true` works beside you");
        master.Should().Contain("What the review or your own run shows missing or wrong goes back to the builder with `send_message`");
        master.Should().Contain("{{ref:code-review-brief}}", "the review brief is a shared reference, inlined at load");
    }

    [Fact]
    public void PackagedCodingMaster_RecordsThePlanWithRecordPlan() =>
        Folded().Should().Contain("Record your plan with `record_plan` beside it")
            .And.NotContain("RunRecordDir", "2026-11-28-db88b: the plan reaches the run page through record_plan");

    [Fact]
    public void PackagedCodingMaster_DeclaresNoFixRoundCounter()
    {
        var master = PackagedMaster.Read("coding-agent-master");

        master.Should().NotContain("MaxFixIterations", "3.0.0 keeps going while rounds make progress");
        master.Should().Contain("## Ticket instructions", "the heading the skills repository's validate-skills.sh requires");
        Folded().Should().Contain("when they stop making progress, report the red honestly with the failing output");
    }

    /// <summary>2026-11-18-98fa: the master's report is markdown, written as the text of its closing reply.
    /// 2026-11-22-96c8b: 3.5.0 (skills v5.42.0) ends on the report as its last reply, names no finish_spec, gives the
    /// report's read its headings and says a later pass's report is added to the earlier one.</summary>
    [Fact]
    public void PackagedCodingMaster_ReportsInMarkdown_AsItsLastReply_AndNamesNoFinishSpec()
    {
        var master = Folded();
        master.Should().Contain("Your report is the text of your closing reply, written in markdown")
            .And.Contain("Your report is your last reply, and nothing else closes the spec")
            .And.Contain("it is added to what you reported before")
            .And.Contain("**Follow-ups**").And.Contain("**Ignored instructions**").And.Contain("**Build note**")
            .And.Contain("**Memories relied on**")
            .And.Contain("a question only a person can answer goes through `ask_human`");
        master.Should().NotContain("finish_spec").And.NotContain("```verdict", "the fenced verdict block is gone");
    }
}
