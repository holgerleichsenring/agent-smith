using System;
using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0273: the keystone gates on REGRESSIONS (green→red), not on any red. When the
/// agent reports the raw failing-test lists, the framework computes new-failures =
/// final \ baseline; a test already red at HEAD does not fail the run. Absent
/// lists → the original binary gate (back-compat).
/// </summary>
public sealed class RunOutcomeKeystoneRegressionTests
{
    private static KeystoneVerdict Evaluate(MasterVerification v) =>
        RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: v, ratifiedCriteria: Array.Empty<string>());

    [Fact]
    public void PreExistingRedInBaseline_DoesNotBlock()
    {
        // Agent's own change builds; the only red test was already red at HEAD.
        var v = new MasterVerification(
            VerificationStatus.Failed, BuildRan: true, BuildPassed: true,
            TestsRan: true, TestsPassed: false, Summary: "pre-existing red unrelated",
            FailingTests: new[] { "UserPermissionContextCalculatorTests.Calc" },
            BaselineFailingTests: new[] { "UserPermissionContextCalculatorTests.Calc" });

        Evaluate(v).Satisfied.Should().BeTrue();
    }

    [Fact]
    public void NewRegression_Blocks_AndNamesIt()
    {
        var v = new MasterVerification(
            VerificationStatus.Failed, BuildRan: true, BuildPassed: true,
            TestsRan: true, TestsPassed: false, Summary: "broke a test",
            FailingTests: new[] { "Pre.Existing", "New.Broken" },
            BaselineFailingTests: new[] { "Pre.Existing" });

        var verdict = Evaluate(v);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("New.Broken").And.Contain("NEW");
    }

    [Fact]
    public void BuildFailed_Blocks_EvenWithNoRegressions()
    {
        var v = new MasterVerification(
            VerificationStatus.Failed, BuildRan: true, BuildPassed: false,
            TestsRan: false, TestsPassed: false, Summary: "build broke",
            FailingTests: System.Array.Empty<string>(),
            BaselineFailingTests: System.Array.Empty<string>());

        var verdict = Evaluate(v);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("build");
    }

    [Fact]
    public void AllTestsGreen_WithEmptyFailingList_Ok()
    {
        var v = new MasterVerification(
            VerificationStatus.Green, BuildRan: true, BuildPassed: true,
            TestsRan: true, TestsPassed: true, Summary: "ok",
            FailingTests: System.Array.Empty<string>());

        Evaluate(v).Satisfied.Should().BeTrue();
    }

    [Fact]
    public void NoFailingTestsList_FailedStatus_FallsBackToBinaryBlock()
    {
        // Older skill: no lists reported → original binary behaviour.
        var v = new MasterVerification(
            VerificationStatus.Failed, BuildRan: true, BuildPassed: true,
            TestsRan: true, TestsPassed: false, Summary: "red");

        Evaluate(v).Satisfied.Should().BeFalse();
    }

    [Fact]
    public void NoFailingTestsList_Green_Ok()
    {
        var v = new MasterVerification(
            VerificationStatus.Green, BuildRan: true, BuildPassed: true,
            TestsRan: true, TestsPassed: true, Summary: "ok");

        Evaluate(v).Satisfied.Should().BeTrue();
    }
}
