using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

// p0406: the acceptance gate the open loop re-engages against. A knowledge phase
// (ships_code: false) ships no source, so it is judged by its dispositions alone —
// run fa8c spent 41 minutes and $5.88 failing to reach a green build it had declared
// it would never produce.
// 2026-09-06-9f14: the gate takes the criteria themselves and pairs each disposition
// with the one it names; the "criterion N" answers below name nothing that matches,
// so they exercise the positional fallback — the pairing every answer got before.
public sealed class MasterAcceptanceGateTests
{
    private static readonly IReadOnlyList<string> Criteria =
    [
        "The endpoint returns 404 for an unknown id.",
        "Existing callers keep their behaviour.",
        "A failing lookup is logged once.",
    ];

    private static IReadOnlyList<string> CriteriaOf(int count) => Criteria.Take(count).ToList();

    private static MasterVerification Verdict(VerificationStatus status, params AcceptanceStatus[] dispositions) =>
        new(status, BuildRan: status != VerificationStatus.Unknown,
            BuildPassed: status != VerificationStatus.Failed, TestsRan: false, TestsPassed: false, "summary",
            AcceptanceDispositions: dispositions
                .Select((d, i) => new AcceptanceDisposition($"criterion {i}", d, "evidence"))
                .ToList());

    private static MasterVerification Green(params AcceptanceDisposition[] dispositions) =>
        new(VerificationStatus.Green, true, true, true, true, "summary", AcceptanceDispositions: dispositions);

    private static AcceptanceDisposition Met(string criterion) => new(criterion, AcceptanceStatus.Met, "an edit");

    private static AcceptanceDisposition Unmet(string criterion) => new(criterion, AcceptanceStatus.Unmet, "");

    [Fact]
    public void ObjectivelySatisfied_NoSourceChanged_AllCriteriaMet_NoBuildStatus_IsSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Unknown, AcceptanceStatus.Met, AcceptanceStatus.Met),
            CriteriaOf(2), producedSourceChanges: false)
            .Should().BeTrue("a phase that changed no source has no build for the verdict to be green about");

    [Fact]
    public void ObjectivelySatisfied_SourceChanged_SameVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Unknown, AcceptanceStatus.Met, AcceptanceStatus.Met),
            CriteriaOf(2), producedSourceChanges: true)
            .Should().BeFalse(
                "a phase that produced source still owes a green build — p0421 reads that from "
                + "the run's changes instead of a ships_code declaration, and an Unknown verdict "
                + "over changed code is exactly the hollow success this gate exists for");

    [Fact]
    public void ObjectivelySatisfied_NoSourceChanged_OwnRedVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Failed, AcceptanceStatus.Met),
            CriteriaOf(1), producedSourceChanges: false)
            .Should().BeFalse("the master reporting its own work failed is not overruled by what it did or did not touch");

    [Fact]
    public void ObjectivelySatisfied_NoSourceChanged_UnmetCriterion_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.NoTests, AcceptanceStatus.Met, AcceptanceStatus.Unmet),
            CriteriaOf(2), producedSourceChanges: false)
            .Should().BeFalse();

    [Fact]
    public void ObjectivelySatisfied_NoSourceChanged_FewerDispositionsThanCriteria_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.NoTests, AcceptanceStatus.Met),
            CriteriaOf(3), producedSourceChanges: false)
            .Should().BeFalse();

    [Fact]
    public void ObjectivelySatisfied_NotApplicableWithEvaluatedReason_Counts() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(VerificationStatus.Green, AcceptanceStatus.NotApplicable),
            CriteriaOf(1), producedSourceChanges: false)
            .Should().BeTrue();

    [Fact]
    public void ObjectivelySatisfied_NoCriteria_IsSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(null, CriteriaOf(0), producedSourceChanges: true).Should().BeTrue();

    [Fact]
    public void ObjectivelySatisfied_NoVerdict_IsNotSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(null, CriteriaOf(1), producedSourceChanges: true).Should().BeFalse();

    // ---- 2026-09-06-9f14: the pairing is by the criterion each disposition names ----

    // Red on main: the index walk measured criterion 2 against the surplus entry and
    // re-drove a run that had met every ratified criterion.
    [Fact]
    public void ObjectivelySatisfied_ALongerAnswerInAnotherOrder_EveryCriterionMet_IsSatisfied() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Green(Met(Criteria[1]), Unmet("A criterion the master made up."), Met(Criteria[0])),
            CriteriaOf(2), producedSourceChanges: true)
            .Should().BeTrue("both ratified criteria carry a met disposition; the surplus entry names nothing ratified");

    // A pure permutation of a complete answer is invisible to the boolean — the set of
    // statuses is the same in any order — so what the pairing changes is WHICH criterion is
    // reported unmet, and that is what the judgement renders.
    [Fact]
    public void Judge_AnAnswerInAnotherOrder_NamesTheCriterionThatIsUnmet()
    {
        var judgement = MasterAcceptanceGate.Judge(
            Green(Unmet(Criteria[1]), Met(Criteria[0])), CriteriaOf(2), producedSourceChanges: true);

        judgement.Satisfied.Should().BeFalse();
        judgement.Pairings.Select(p => (p.Criterion, p.Satisfied, p.By)).Should().Equal(
            (Criteria[0], true, PairedBy.Name),
            (Criteria[1], false, PairedBy.Name));
    }

    [Fact]
    public void ObjectivelySatisfied_ANamelessAnswerThatSatisfiedTheGateBefore_StillSatisfiesIt() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Green(Met("criterion 1"), Met("criterion 2"), Met("criterion 3")),
            CriteriaOf(3), producedSourceChanges: true)
            .Should().BeTrue("every name misses, so every disposition keeps the slot its position gives it");

    [Fact]
    public void Judge_AShortAnswer_RefusesBeforePairing()
    {
        var judgement = MasterAcceptanceGate.Judge(
            Green(Met(Criteria[0]), Met(Criteria[1])), CriteriaOf(3), producedSourceChanges: true);

        judgement.Satisfied.Should().BeFalse("a master that did not address every criterion is not done");
        judgement.Pairings.Should().BeEmpty();
        judgement.Describe().Should().Contain("2 disposition(s) for 3 criteria");
    }

    [Fact]
    public void Judge_AFallbackPairing_IsRenderedForTheTrail()
    {
        var judgement = MasterAcceptanceGate.Judge(
            Green(Met(Criteria[0]), Met("criterion 2")), CriteriaOf(2), producedSourceChanges: true);

        judgement.Describe().Should().Be(
            "2 of 2 criteria satisfied: #1 met (by name); "
            + "#2 met (by position — the disposition named nothing that matches)");
    }

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
