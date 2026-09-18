using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.PremiseCheckTestDoubles;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79c: a phase states what it rests on, and before it is worked a fresh instance
/// checks those premises against the repositories. A premise proven false on a look this check
/// took stops the phase and is handed back; anything the framework cannot verify stops nothing.
/// </summary>
public sealed class PremiseCheckTests
{
    private const string Falsified =
        """
        [{"premise": "OrderHandler validates the payload before dispatch",
          "verdict": "no-longer-holds", "why": "nothing in the handler validates any more",
          "cites": "M1"}]
        """;

    [Fact]
    public async Task PremiseCheck_PhaseDraftedBeforeThePin_IsSkippedWithAReason()
    {
        var h = For(DraftWithoutPremises(), "[]");

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("skipped").And.Contain("states no premises of its own");
        h.Provider.Turns.Should().Be(0, "nothing was asked, so nothing was paid for");
    }

    [Fact]
    public async Task PremiseCheck_FactFalsifiedByItsOwnRead_FailsThePhaseWithTheEvidence()
    {
        // The verdict has to carry the step a reader must judge: WHICH premise, WHAT was looked
        // at, and the framework's own minted line for that look. Without the middle one a
        // hand-back says "a premise is false, trust me".
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "ValidatePayload"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeFalse();
        result.Message.Should()
            .Contain("OrderHandler validates the payload before dispatch", "the premise")
            .And.Contain($"it looked at {DerivationTestLooks.Repo}: ",
                "what the check actually looked at, carried onto the finding in its own right")
            .And.Contain("ValidatePayload", "and the term it looked for")
            .And.Contain("[M1]", "the id the framework minted for the look this check took")
            .And.Contain("premise check ran", "the minted line names its own actor");
    }

    [Fact]
    public async Task PremiseCheck_PremiseThePhaseNeverStated_IsNotAdmittedHoweverItIsCited()
    {
        // The attack this closes: look once at anything, then report a premise you made up.
        const string Invented =
            """
            [{"premise": "the payload is serialised with a legacy formatter",
              "verdict": "no-longer-holds", "why": "no formatter is registered", "cites": "M1"}]
            """;
        var h = For(Draft(), Invented, searches: (DerivationTestLooks.Repo, "Formatter"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue("a premise the phase never stated cannot stop it");
        result.Message.Should().Contain("could not be proven either way");
    }

    [Fact]
    public async Task PremiseCheck_PremiseParaphrased_IsResolvedToTheSpecsOwnWording()
    {
        // A paraphrase is admitted — it is the same premise — but the verdict quotes the SPEC,
        // so a hand-back never hands somebody the model's words as if the spec had said them.
        const string Paraphrased =
            """
            [{"premise": "OrderHandler validates the payload",
              "verdict": "no-longer-holds", "why": "the method is gone", "cites": "M1"}]
            """;
        var h = For(Draft(), Paraphrased, searches: (DerivationTestLooks.Repo, "Validate"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("OrderHandler validates the payload before dispatch",
            "the spec's wording, not the checker's");
    }

    [Fact]
    public async Task PremiseCheck_DerivedPhaseWhoseOnlyDecisionIsTheDerivers_IsSkippedAndCostsNothing()
    {
        // Every derived phase carries that decision, and it names a RUN ARTIFACT that exists in
        // no sandbox — left in, it would be the one premise every derived run pays to check and
        // then fails to find.
        var h = For(DerivedDraft(), Falsified, searches: (DerivationTestLooks.Repo, "anything"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("states no premises of its own");
        h.Provider.Turns.Should().Be(0, "a derived phase that states nothing of its own costs no call");
    }

    [Fact]
    public async Task PremiseCheck_AnswerThatCouldNotBeRead_SaysSoInsteadOfSayingSkipped()
    {
        var h = For(Draft(), "I could not work this out.", searches: (DerivationTestLooks.Repo, "Validate"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue("failing open is right");
        result.Message.Should().Contain("could not be taken")
            .And.NotContain("skipped", "an operator must tell a paid call apart from a decision not to ask");
    }

    [Fact]
    public async Task PremiseCheck_FindingWithoutAnEvidenceId_IsRecordedUnprovenAndRunsOn()
    {
        const string NoCitation =
            """
            [{"premise": "OrderHandler validates the payload before dispatch",
              "verdict": "no-longer-holds", "why": "I do not think it does", "cites": null}]
            """;
        var h = For(Draft(), NoCitation, searches: (DerivationTestLooks.Repo, "Validate"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue("a statement nobody can check stops nothing");
        result.Message.Should().Contain("could not be proven either way")
            .And.Contain("OrderHandler validates the payload before dispatch");
    }

    [Fact]
    public async Task PremiseCheck_FindingOnALookThatCouldNotRun_IsRecordedUnprovenAndRunsOn()
    {
        // A search that exits other than 0 or 1 still mints an id; its line says it proves
        // nothing, and a broken read proves nothing in either direction.
        var h = For(Draft(), Falsified, exitCode: 127, searches: (DerivationTestLooks.Repo, "Validate"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("could not be proven either way");
    }

    [Fact]
    public async Task PremiseCheck_FindingCitingTheDeriversEvidenceId_IsNotAdmitted()
    {
        // L is the derivation's letter and the phase's own facts carry L-ids. An answer that
        // cites one is citing the derivation's look, not a look this check took.
        var h = For(Draft(),
            Falsified.Replace("\"M1\"", "\"L2\"", StringComparison.Ordinal),
            searches: (DerivationTestLooks.Repo, "Validate"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("could not be proven either way");
    }

    [Fact]
    public async Task PremiseCheck_FindingRestatingAnUnmetCriterion_IsDiscarded()
    {
        // The account asks whether the done-list is satisfied, before and after the work. A
        // finding that merely repeats an unmet criterion is that question, not this one.
        const string Restatement =
            """
            [{"premise": "the dispatch no longer runs inside the handler",
              "verdict": "no-longer-holds", "why": "it still runs there", "cites": "M1"}]
            """;
        var h = For(Draft(), Restatement, searches: (DerivationTestLooks.Repo, "Dispatch"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("every stated premise still holds")
            .And.NotContain("could not be proven", "it was discarded, not recorded unproven");
    }

    [Fact]
    public async Task PremiseCheck_PremiseSharingWordingWithACriterion_IsStillReportable()
    {
        // The discard is bounded to a VERBATIM restatement. This premise is a contiguous run of
        // the criterion's words covering most of it — exactly what the word-run matcher admits —
        // and it is a fact the phase states, so it must still reach a verdict. A premise that
        // can never be reported, not even as unproven, is a blind spot nobody can see into.
        var draft = Draft(
            fact: "the outbox table has a dispatched_at column",
            assumption: "", decision: "no decision",
            done: "the outbox table has a dispatched_at column and every dispatch sets it");
        const string Overlapping =
            """
            [{"premise": "the outbox table has a dispatched_at column",
              "verdict": "no-longer-holds", "why": "the column was dropped last week", "cites": "M1"}]
            """;
        var h = For(draft, Overlapping, searches: (DerivationTestLooks.Repo, "dispatched_at"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeFalse("it is a premise the phase states, not a criterion");
        result.Message.Should().Contain("the outbox table has a dispatched_at column");
    }

    [Fact]
    public async Task PremiseCheck_ClaimInADecisionThatTheCodeContradicts_IsAFalsePremise()
    {
        const string DecisionIsWrong =
            """
            [{"premise": "The sender stays in Api.Orders because OrderHandler.cs:34-41 already owns dispatch.",
              "verdict": "no-longer-holds", "why": "OrderHandler.cs holds no dispatch at those lines",
              "cites": "M1"}]
            """;
        var h = For(Draft(), DecisionIsWrong, searches: (DerivationTestLooks.Repo, "OrderHandler"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeFalse("a decision's prose states premises too");
        result.Message.Should().Contain("OrderHandler.cs:34-41");
    }

    [Fact]
    public async Task PremiseCheck_AllPremisesHold_SplicesNothingAndTheMasterRuns()
    {
        var h = For(Draft(), "[]", searches: (DerivationTestLooks.Repo, "Validate"));

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().BeNullOrEmpty("the check reports; it never splices work");
        result.DropAhead.Should().BeNullOrEmpty();
        h.Events.Events.OfType<Contracts.Events.PhaseStateChangedEvent>().Should().BeEmpty(
            "a phase whose premises hold keeps the standing SelectPhase gave it");
    }

    [Fact]
    public async Task PremiseCheck_CostCapExhausted_IsSkippedWithAReason()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));
        var tracker = new Application.Services.PipelineCostTracker(
            costCap: new Contracts.Models.Configuration.CostCapValues { Usd = 1000m, Tokens = 1 });
        tracker.Track(new Microsoft.Extensions.AI.ChatResponse(
            new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant, "spent"))
        {
            ModelId = "unpriced-model",
            Usage = new Microsoft.Extensions.AI.UsageDetails { InputTokenCount = 10_000 },
        });
        tracker.IsBudgetExhausted.Should().BeTrue("the premise: the run really is over its cap");
        h.Pipeline.Set("PipelineCostTracker", tracker);

        var result = await h.RunAsync();

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("cost cap is exhausted");
        h.Provider.Turns.Should().Be(0);
    }

    [Fact]
    public async Task PremiseCheck_NeverWritesASpecSetRevision()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));
        var before = h.Pipeline.Get<SpecSet>(ContextKeys.SpecSet);

        await h.RunAsync();

        h.Pipeline.Get<SpecSet>(ContextKeys.SpecSet).Should().BeSameAs(before);
        before.Revisions.Should().BeEmpty("the check reports; an amendment is made elsewhere");
    }

    [Fact]
    public async Task PremiseCheck_OneCallPerPhase_NoSecondPass()
    {
        var h = For(Draft(), Falsified, searches: (DerivationTestLooks.Repo, "Validate"));

        await h.RunAsync();

        h.Provider.Turns.Should().Be(2, "one look and one answer — and no second question");
        h.Provider.Prompts.Should().ContainSingle("the checker opens exactly one conversation");
    }
}
