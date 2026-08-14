using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

// p0406: the acceptance gate the open loop re-engages against. A knowledge phase
// (ships_code: false) ships no source, so it is judged by its dispositions alone —
// run fa8c spent 41 minutes and $5.88 failing to reach a green build it had declared
// it would never produce.
public sealed class MasterAcceptanceGateTests
{
    private static MasterVerification Verdict(VerificationStatus status, params AcceptanceStatus[] dispositions) =>
        new(status, BuildRan: status != VerificationStatus.Unknown,
            BuildPassed: status != VerificationStatus.Failed, TestsRan: false, TestsPassed: false, "summary",
            AcceptanceDispositions: dispositions
                .Select((d, i) => new AcceptanceDisposition($"criterion {i}", d, "evidence"))
                .ToList());

    [Fact]
    public void ObjectivelySatisfied_NoSourceChanged_AllCriteriaMet_NoBuildStatus_IsSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Unknown, AcceptanceStatus.Met, AcceptanceStatus.Met),
            criteriaCount: 2, producedSourceChanges: false)
            .Should().BeTrue("a phase that changed no source has no build for the verdict to be green about");

    [Fact]
    public void ObjectivelySatisfied_SourceChanged_SameVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Unknown, AcceptanceStatus.Met, AcceptanceStatus.Met),
            criteriaCount: 2, producedSourceChanges: true)
            .Should().BeFalse(
                "a phase that produced source still owes a green build — p0421 reads that from "
                + "the run's changes instead of a ships_code declaration, and an Unknown verdict "
                + "over changed code is exactly the hollow success this gate exists for");

    [Fact]
    public void ObjectivelySatisfied_NoSourceChanged_OwnRedVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Failed, AcceptanceStatus.Met),
            criteriaCount: 1, producedSourceChanges: false)
            .Should().BeFalse("the master reporting its own work failed is not overruled by what it did or did not touch");

    [Fact]
    public void ObjectivelySatisfied_NoSourceChanged_UnmetCriterion_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.NoTests, AcceptanceStatus.Met, AcceptanceStatus.Unmet),
            criteriaCount: 2, producedSourceChanges: false)
            .Should().BeFalse();

    [Fact]
    public void ObjectivelySatisfied_NoSourceChanged_FewerDispositionsThanCriteria_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.NoTests, AcceptanceStatus.Met),
            criteriaCount: 3, producedSourceChanges: false)
            .Should().BeFalse();

    [Fact]
    public void ObjectivelySatisfied_NotApplicableWithEvaluatedReason_Counts() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Green, AcceptanceStatus.NotApplicable),
            criteriaCount: 1, producedSourceChanges: false)
            .Should().BeTrue();

    [Fact]
    public void ObjectivelySatisfied_NoCriteria_IsSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(null, criteriaCount: 0, producedSourceChanges: true).Should().BeTrue();

    [Fact]
    public void ObjectivelySatisfied_NoVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(null, criteriaCount: 1, producedSourceChanges: true).Should().BeFalse();

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(7, true)]
    public void VerdictlessAfterOneRedrive_NullVerdict_BitesFromTheSecondPass(int pass, bool expected) =>
        MasterAcceptanceGate.VerdictlessAfterOneRedrive(null, pass, criteriaCount: 4)
            .Should().Be(expected);

    [Fact]
    public void VerdictlessAfterOneRedrive_AVerdictExists_NeverBites() =>
        MasterAcceptanceGate.VerdictlessAfterOneRedrive(
            Verdict(VerificationStatus.Green, AcceptanceStatus.Unmet), reengagePass: 9, criteriaCount: 1)
            .Should().BeFalse("an unmet contract with a real verdict is still worth re-driving");

    [Fact]
    public void VerdictlessAfterOneRedrive_NoContract_NeverBites() =>
        MasterAcceptanceGate.VerdictlessAfterOneRedrive(null, reengagePass: 9, criteriaCount: 0)
            .Should().BeFalse("with no criteria the acceptance branch never drives the loop");
}
