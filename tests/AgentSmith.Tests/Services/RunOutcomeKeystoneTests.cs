using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0241/p0244: the keystone is the deterministic regression gate — these pin the
// exact conditions under which a run may (not) be reported as success.
public sealed class RunOutcomeKeystoneTests
{
    private static MasterVerification Green => new(VerificationStatus.Green, true, true, true, true, "ok");

    [Fact]
    public void FixPresetWithNoChangeAtAll_Fails()
    {
        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: false, recordedChange: false, verification: Green);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("no code changes");
    }

    [Fact]
    public void FixPresetRecordedEditsButGitCommittedNothing_Fails()
    {
        // p0244: the write-placement bug — the agent recorded edits but they never
        // reached the working tree, so git committed nothing. Must be a loud
        // failure, never a hollow success masked by the recorded-changes list.
        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: false, recordedChange: true, verification: Green);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("git committed NOTHING");
    }

    [Fact]
    public void FixPresetWithCommitButNoVerdict_Fails()
    {
        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: null);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("did not emit a verification verdict");
    }

    [Fact]
    public void FixPresetWithCommitAndGreenVerdict_Succeeds()
    {
        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: Green);

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void FailedVerdict_Fails()
    {
        var failed = new MasterVerification(VerificationStatus.Failed, true, true, true, false, "tests red");
        var verdict = RunOutcomeKeystone.Evaluate(true, true, gitCommittedChange: true, recordedChange: true, verification: failed);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("FAILED verification");
    }

    [Fact]
    public void TestsRanButNotPassed_Fails()
    {
        var inconsistent = new MasterVerification(VerificationStatus.Green, true, true, TestsRan: true, TestsPassed: false, "x");
        var verdict = RunOutcomeKeystone.Evaluate(true, true, gitCommittedChange: true, recordedChange: true, verification: inconsistent);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("Tests did not pass");
    }

    [Fact]
    public void NoTestsStatus_Succeeds()
    {
        var noTests = new MasterVerification(VerificationStatus.NoTests, true, true, false, false, "repo has no tests");
        var verdict = RunOutcomeKeystone.Evaluate(true, true, gitCommittedChange: true, recordedChange: true, verification: noTests);

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void FixNoTestPreset_CommitPresent_NoVerdictNeeded_Succeeds()
    {
        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: false,
            gitCommittedChange: true, recordedChange: true, verification: null);

        verdict.Satisfied.Should().BeTrue();
    }

    [Fact]
    public void ReadOnlyPreset_ZeroChanges_Succeeds()
    {
        var verdict = RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: false, expectsGreenTests: false,
            gitCommittedChange: false, recordedChange: false, verification: null);

        verdict.Satisfied.Should().BeTrue();
    }
}
