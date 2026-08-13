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
    public void ObjectivelySatisfied_KnowledgePhase_AllCriteriaMet_NoBuildStatus_IsSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Unknown, AcceptanceStatus.Met, AcceptanceStatus.Met),
            criteriaCount: 2, shipsCode: false)
            .Should().BeTrue("a phase that ships no code has no build for the verdict to be green about");

    [Fact]
    public void ObjectivelySatisfied_CodePhase_SameVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Unknown, AcceptanceStatus.Met, AcceptanceStatus.Met),
            criteriaCount: 2, shipsCode: true)
            .Should().BeFalse("a code phase still owes a green build — p0341e behaviour is unchanged");

    [Fact]
    public void ObjectivelySatisfied_KnowledgePhase_OwnRedVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Failed, AcceptanceStatus.Met),
            criteriaCount: 1, shipsCode: false)
            .Should().BeFalse("the master reporting its own work failed is not overruled by the phase kind");

    [Fact]
    public void ObjectivelySatisfied_KnowledgePhase_UnmetCriterion_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.NoTests, AcceptanceStatus.Met, AcceptanceStatus.Unmet),
            criteriaCount: 2, shipsCode: false)
            .Should().BeFalse();

    [Fact]
    public void ObjectivelySatisfied_KnowledgePhase_FewerDispositionsThanCriteria_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.NoTests, AcceptanceStatus.Met),
            criteriaCount: 3, shipsCode: false)
            .Should().BeFalse();

    [Fact]
    public void ObjectivelySatisfied_NotApplicableWithEvaluatedReason_Counts() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Green, AcceptanceStatus.NotApplicable),
            criteriaCount: 1, shipsCode: true)
            .Should().BeTrue();

    [Fact]
    public void ObjectivelySatisfied_NoCriteria_IsSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(null, criteriaCount: 0, shipsCode: true).Should().BeTrue();

    [Fact]
    public void ObjectivelySatisfied_NoVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(null, criteriaCount: 1, shipsCode: false).Should().BeFalse();

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
