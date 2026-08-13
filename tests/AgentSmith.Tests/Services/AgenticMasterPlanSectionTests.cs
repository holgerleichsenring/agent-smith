using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>p0394a: the master's plan section renders the ratified phase spec —
/// goal, steps and done criteria verbatim — so the master executes the same
/// artifact the ledger seeded from and the keystone verifies; absent spec →
/// empty section (scan / spec-dialog surfaces).</summary>
public sealed class AgenticMasterPlanSectionTests
{
    [Fact]
    public void MasterPrompt_PlanSection_RendersSpecGoalStepsDone()
    {
        var draft = new PhaseDraft("p0001", "The endpoint returns 400 on empty payloads", "phase: p0001", [])
        {
            Steps =
            [
                new PhaseStep("guard", "Reject empty payloads in the controller.", "server/src/Api/Controller.cs"),
                new PhaseStep("verify", "Run the suite.", null),
            ],
            Done = ["Empty payloads yield 400 (pinned by test)."],
        };

        var section = MasterPromptSections.BuildPlanSection(draft);

        section.Should().Contain("plan of record");
        section.Should().Contain("The endpoint returns 400 on empty payloads");
        section.Should().Contain("[guard] Reject empty payloads in the controller.");
        section.Should().Contain("(target: server/src/Api/Controller.cs)");
        section.Should().Contain("[verify] Run the suite.");
        section.Should().Contain("- Empty payloads yield 400 (pinned by test).");
        // Spec order is preserved verbatim.
        section.IndexOf("[guard]", System.StringComparison.Ordinal)
            .Should().BeLessThan(section.IndexOf("[verify]", System.StringComparison.Ordinal));
    }

    [Fact]
    public void BuildPlanSection_NoSpec_ReturnsEmpty()
    {
        MasterPromptSections.BuildPlanSection(null).Should().BeEmpty();
    }

    [Fact]
    public void BuildPlanSection_SpecWithoutSteps_StillCarriesGoalAndDone()
    {
        var draft = new PhaseDraft("p0002", "Goal only", "phase: p0002", [])
        {
            Done = ["It is done."],
        };

        var section = MasterPromptSections.BuildPlanSection(draft);

        section.Should().Contain("Goal only");
        section.Should().Contain("- It is done.");
        section.Should().NotContain("Steps:");
    }
}
