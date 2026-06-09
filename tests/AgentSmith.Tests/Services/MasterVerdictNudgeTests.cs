using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0263: when a code-changing run emitted no parseable verdict but a verdict IS
// expected (a green-tests pipeline), the master is re-prompted once to emit it.
// These pin the trigger decision (sibling to MasterApplyDriveTests).
public sealed class MasterVerdictNudgeTests
{
    private static MasterVerification Verdict() =>
        new(VerificationStatus.Green, BuildRan: true, BuildPassed: true,
            TestsRan: true, TestsPassed: true, Summary: "ok");

    [Fact]
    public void ShouldNudge_GreenTestsPipeline_NoVerdict_True()
        => AgenticMasterHandler.ShouldNudgeForVerdict("fix-bug", null).Should().BeTrue();

    [Fact]
    public void ShouldNudge_VerdictAlreadyPresent_False()
        => AgenticMasterHandler.ShouldNudgeForVerdict("fix-bug", Verdict()).Should().BeFalse();

    [Fact]
    public void ShouldNudge_NonGreenTestsPipeline_NoVerdict_False()
        // security-scan is read-only / no green-tests requirement — never nudge.
        => AgenticMasterHandler.ShouldNudgeForVerdict("security-scan", null).Should().BeFalse();

    [Fact]
    public void ShouldNudge_NoPipelineName_False()
        => AgenticMasterHandler.ShouldNudgeForVerdict(null, null).Should().BeFalse();
}
