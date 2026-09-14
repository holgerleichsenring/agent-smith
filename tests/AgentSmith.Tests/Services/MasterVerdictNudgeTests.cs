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
        => MasterReengagementPolicy.ShouldNudgeForVerdict("fix-bug", null, verdictDemandedInPass: false).Should().BeTrue();

    [Fact]
    public void ShouldNudge_VerdictAlreadyPresent_False()
        => MasterReengagementPolicy.ShouldNudgeForVerdict("fix-bug", Verdict(), verdictDemandedInPass: false).Should().BeFalse();

    [Fact]
    public void ShouldNudge_NonGreenTestsPipeline_NoVerdict_False()
        // security-scan is read-only / no green-tests requirement — never nudge.
        => MasterReengagementPolicy.ShouldNudgeForVerdict("security-scan", null, verdictDemandedInPass: false).Should().BeFalse();

    [Fact]
    public void ShouldNudge_NoPipelineName_False()
        => MasterReengagementPolicy.ShouldNudgeForVerdict(null, null, verdictDemandedInPass: false).Should().BeFalse();

    // 2026-09-08-805f: a demand the ledger-complete brake appended inside the pass is this
    // re-drive; the handler does not ask a second time.
    [Fact]
    public void ShouldNudgeForVerdict_DemandedInPass_False()
        => MasterReengagementPolicy.ShouldNudgeForVerdict("fix-bug", null, verdictDemandedInPass: true).Should().BeFalse();
}
