using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>2026-11-09-0620f: the server texts a coding run reads agree with its stop rule, its verdict and its tools.</summary>
public sealed class CodingRunTextTests
{
    [Fact]
    public void MasterNudges_Apply_AsksForTheEditsOrTheBlockerNotAnySourceFile()
    {
        var nudge = MasterNudges.BuildApplyNudge("TASK", ProgressLedger.Empty);

        nudge.Should().Contain("not made the changes it names").And.Contain("the report says what is missing and why")
            .And.Contain(MasterNudges.ReportAgain).And.NotContain("finish_spec")
            .And.NotContain("at least one SOURCE file").And.EndWith("TASK");
    }

    [Fact]
    public void MasterClosing_Coding_EndsOnTheReport()
    {
        MasterClosing.For("coding-agent-master", new PipelineContext()).Should()
            .Contain("Your last reply is your report").And.NotContain("finish_spec").And.NotContain("summarise what changed");
    }

    [Fact]
    public void AskHuman_RecommendedChoice_IsPostedWithTheQuestion()
    {
        var host = new TicketClarificationToolHost();

        host.AskHuman("Which store?", answerer: "target", choices: [new("SQL"), new("Blob")], recommended_index: 1);

        host.Asked[0].Question.Should().EndWith("Recommended: Blob");
    }
}
