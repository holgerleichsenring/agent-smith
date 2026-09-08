using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-06-3d81: a criterion the repository cannot make true is answered, kept, and shown.
/// <para>
/// The answer existed (not_applicable with the evaluated meaning) and the gate accepted it;
/// what was missing was anyone being told about it and anyone reading it afterwards. The
/// verdict is overwritten by every phase's pass, so the declined criteria are kept in their
/// own per-phase ledger and rendered from there.
/// </para>
/// </summary>
public sealed class DeclinedCriterionReportTests
{
    private const string Lint = "`npm run lint` exits 0 in the frontend workspace";
    private const string Reason =
        "the eslint config is a shared package this repository does not own; its rules flag production "
        + "code this run never touched — nothing here can be edited to make the command green";

    private static MasterVerification Verdict(params AcceptanceDisposition[] dispositions) =>
        new(VerificationStatus.Green, true, true, true, true, "summary", AcceptanceDispositions: dispositions);

    private static PipelineContext InPhase(string phaseId)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PhaseSpec, new PhaseDraft(phaseId, "goal", "phase: " + phaseId, []) { Done = [Lint] });
        return pipeline;
    }

    [Fact]
    public void Contract_ADeclinedCriterionWithItsReason_SatisfiesTheGate() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(new AcceptanceDisposition(Lint, AcceptanceStatus.NotApplicable, Reason)),
            criteriaCount: 1, producedSourceChanges: true)
            .Should().BeTrue("the third answer has always counted when it carries its evaluated meaning");

    [Fact]
    public void Contract_ADeclinedCriterionWithNoReason_DoesNotSatisfyTheGate() =>
        MasterAcceptanceGate.ObjectivelySatisfied(
            Verdict(new AcceptanceDisposition(Lint, AcceptanceStatus.NotApplicable, "  ")),
            criteriaCount: 1, producedSourceChanges: true)
            .Should().BeFalse("a bare N/A is a refusal, not an answer");

    [Fact]
    public void Ledger_ADeclinedCriterionWithItsReason_IsKeptUnderItsPhase()
    {
        var pipeline = InPhase("p1");

        DeclinedCriteriaLedger.Record(pipeline, Verdict(
            new AcceptanceDisposition("the guard exists", AcceptanceStatus.Met, "Guard.cs"),
            new AcceptanceDisposition(Lint, AcceptanceStatus.NotApplicable, Reason)));

        DeclinedCriteriaLedger.Current(pipeline).All.Should().ContainSingle()
            .Which.Should().Be(new DeclinedCriterion(Lint, Reason, "p1"));
    }

    [Fact]
    public void Ledger_ABareNotApplicable_IsNotKept()
    {
        var pipeline = InPhase("p1");

        DeclinedCriteriaLedger.Record(pipeline, Verdict(
            new AcceptanceDisposition(Lint, AcceptanceStatus.NotApplicable, "")));

        DeclinedCriteriaLedger.Current(pipeline).All.Should().BeEmpty("the ledger carries answers, not refusals");
    }

    [Fact]
    public void Ledger_ASecondPhase_KeepsTheFirstPhasesDeclinedCriterion()
    {
        var pipeline = InPhase("p1");
        DeclinedCriteriaLedger.Record(pipeline, Verdict(
            new AcceptanceDisposition(Lint, AcceptanceStatus.NotApplicable, Reason)));
        pipeline.Set(ContextKeys.PhaseSpec, new PhaseDraft("p2", "goal", "phase: p2", []) { Done = ["callers moved"] });

        DeclinedCriteriaLedger.Record(pipeline, Verdict(
            new AcceptanceDisposition("callers moved", AcceptanceStatus.Met, "Callers.cs")));

        DeclinedCriteriaLedger.Current(pipeline).All.Should().ContainSingle()
            .Which.PhaseId.Should().Be("p1", "the verdict is overwritten per pass; the ledger is not");
    }

    [Fact]
    public void Ledger_ThePhaseRunAgain_ReplacesItsOwnEntries()
    {
        var pipeline = InPhase("p1");
        DeclinedCriteriaLedger.Record(pipeline, Verdict(
            new AcceptanceDisposition(Lint, AcceptanceStatus.NotApplicable, Reason)));

        DeclinedCriteriaLedger.Record(pipeline, Verdict(
            new AcceptanceDisposition(Lint, AcceptanceStatus.Met, "config pinned")));

        DeclinedCriteriaLedger.Current(pipeline).All.Should().BeEmpty("a re-run that met the criterion declined nothing");
    }

    [Fact]
    public void Outcome_ADeclinedCriterion_RendersWithItsReasonAndPhase()
    {
        var pipeline = InPhase("p1");
        DeclinedCriteriaLedger.Record(pipeline, Verdict(
            new AcceptanceDisposition(Lint, AcceptanceStatus.NotApplicable, Reason)));

        var section = DeclinedCriteriaSection.Build(pipeline);

        section.Should().Contain(DeclinedCriteriaSection.Heading)
            .And.Contain($"**{Lint}** — {Reason}")
            .And.Contain("_(phase p1)_");
    }

    [Fact]
    public void Outcome_ARunWithNothingDeclined_RendersNoSuchSection()
    {
        var pipeline = InPhase("p1");
        DeclinedCriteriaLedger.Record(pipeline, Verdict(
            new AcceptanceDisposition(Lint, AcceptanceStatus.Met, "config pinned")));

        DeclinedCriteriaSection.Build(pipeline).Should().BeEmpty();
        DeclinedCriteriaSection.Build(new PipelineContext()).Should().BeEmpty();
    }

    [Fact]
    public void Outcome_ADeclinedCriterion_IsListedOnTheAcceptanceView()
    {
        var accounts = RunAccounts.Empty.With("p1", [new SpecAccount("primary",
            [new CriterionAccount(Lint, AccountDisposition.NotSatisfied, null, "the command exits 1")])]);

        var json = RunStorySnapshotBuilder.BuildAcceptanceJson(
            null, null, accounts, [new DeclinedCriterion(Lint, Reason, "p1")]);
        var view = RunStoryJson.TryDeserialize<AcceptanceView>(json)!;

        view.Criteria.Should().ContainSingle().Which.Status.Should().Be(AcceptanceCriterionStatuses.Unmet,
            "the account judges the branch on its own and its row stays what it was");
        view.Declined.Should().ContainSingle().Which.Should().Be(new DeclinedCriterionView(Lint, Reason, "p1"));
    }

    [Fact]
    public void Outcome_ARunJudgedByNothingElse_StillListsWhatItDeclined()
    {
        var json = RunStorySnapshotBuilder.BuildAcceptanceJson(
            null, null, RunAccounts.Empty, [new DeclinedCriterion(Lint, Reason, "p1")]);

        var view = RunStoryJson.TryDeserialize<AcceptanceView>(json)!;
        view.Criteria.Should().BeEmpty();
        view.Declined.Should().ContainSingle();
    }

    [Fact]
    public void Outcome_ARunWithNothingDeclined_CarriesNoDeclinedList()
    {
        RunStorySnapshotBuilder.BuildAcceptanceJson(null, null, RunAccounts.Empty, []).Should().BeNull();
    }
}
