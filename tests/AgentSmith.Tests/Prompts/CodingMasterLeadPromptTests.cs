using System.Text.RegularExpressions;
using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// 2026-11-18-4f3e: the pinned coding master (3.0.0, skills v5.36.0) leads a sub-agent that builds the spec, writes
/// the plan into the run record directory and reports per done line — and the verdict block it shows parses, field
/// for field, into the MasterVerification the backend of 2026-11-18-c8e2b reads.
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

    [Fact]
    public void PackagedCodingMaster_VerdictExample_ParsesFieldForField()
    {
        var block = Regex.Match(PackagedMaster.Read("coding-agent-master"), "```verdict\\s*\\{[\\s\\S]*?\\}\\s*```").Value;
        block.Should().NotBeEmpty("the master shows the verdict block it ends with");

        var verdict = MasterVerificationParser.TryParse(block)!;

        verdict.Status.Should().Be(VerificationStatus.Green);
        verdict.BuildRan.Should().BeTrue();
        verdict.BuildPassed.Should().BeTrue();
        verdict.TestsRan.Should().BeTrue();
        verdict.TestsPassed.Should().BeTrue();
        verdict.FailingTests.Should().BeEmpty();
        verdict.BaselineFailingTests.Should().BeEmpty();
        verdict.IgnoredInstructions.Should().BeEmpty();
        verdict.AcceptanceDispositions!.Select(d => d.Status).Should().Equal(AcceptanceStatus.Met, AcceptanceStatus.NotMet);
        verdict.AcceptanceDispositions![0].Citations.Should().HaveCount(2);
        verdict.AcceptanceDispositions![1].Evidence.Should().NotBeEmpty("a not-met line carries its why");
        verdict.FixedInPassing.Should().ContainSingle();
        verdict.FollowUps.Should().ContainSingle().Which.Why.Should().NotBeEmpty();
        verdict.Summary.Should().NotBeNullOrEmpty();
        MasterVerificationParser.TryParseBlockedClaim(block).Should().Be(new MasterBlockedClaim(false, ""));
    }
}
