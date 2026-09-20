using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ec: the design prompt says whether the turn may propose, the refusal has its
/// own nudge, and a kept turn records its kind.
/// </summary>
public sealed class SpecDialogDiscussionPromptTests
{
    private static PipelineContext With(params SpecDialogTurn[] transcript)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, transcript);
        return pipeline;
    }

    [Fact]
    public void DesignPrompt_TurnThatMayNotPropose_SaysSo()
    {
        var prompt = new SpecDialogPromptFactory().Build(With(new SpecDialogTurn("user", "update everything")), 0, 0);

        prompt.Should().Contain("MAY NOT propose").And.Contain("When the operator asks for work, answer with")
            .And.Contain("the edge cases").And.Contain("A question is answered as it is asked")
            .And.NotContain("MAY propose");
    }

    [Fact]
    public void DesignPrompt_TurnThatMayPropose_SaysSo()
    {
        var prompt = new SpecDialogPromptFactory().Build(With(
            new SpecDialogTurn("user", "update everything"),
            new SpecDialogTurn("assistant", "found three libraries", SpecDialogTurnKind.Answer),
            new SpecDialogTurn("user", "all three, one phase")), 0, 0);

        prompt.Should().Contain("MAY propose").And.NotContain("MAY NOT propose");
    }

    // 2026-09-17-042ed: the confirmation the findings were shown in is posted to the thread and
    // never appended to the transcript, so the edit turn is shown them here or nowhere.
    [Fact]
    public void EditTurn_AfterAReviewedProposal_IsShownItsFindings()
    {
        var pipeline = With(new SpecDialogTurn("user", "cut it in two"));
        pipeline.Set<OutcomeProposal>(ContextKeys.SpecDialogRevisedProposal,
            new PhaseOutcome(new PhaseDraft("p9999", "widget goal", "phase: p9999", [])) with
            {
                Findings =
                [
                    new ProposalFinding("p9999", "false premise", "the endpoint is already there",
                        Quote: null, Evidence: "[P1] repo-a: the proposal review ran 'read src/Api.cs' exited 0"),
                ],
            });

        var prompt = new SpecDialogPromptFactory().Build(pipeline, 0, 0);

        prompt.Should().Contain("What the review of your last proposal found")
            .And.Contain("p9999 — false premise: the endpoint is already there")
            .And.Contain("[P1] repo-a: the proposal review ran 'read src/Api.cs' exited 0");
    }

    [Fact]
    public void Turn_AfterAFiledProposal_IsShownNoFindings()
    {
        var reviewedClean = With(new SpecDialogTurn("user", "and now the next one"));
        reviewedClean.Set<OutcomeProposal>(ContextKeys.SpecDialogRevisedProposal,
            new PhaseOutcome(new PhaseDraft("p9999", "widget goal", "phase: p9999", [])));

        new SpecDialogPromptFactory().Build(With(new SpecDialogTurn("user", "and now the next one")), 0, 0)
            .Should().NotContain("the review of your last proposal",
                "only an edit turn is handed a proposal, and only the router knows a turn is one");
        new SpecDialogPromptFactory().Build(reviewedClean, 0, 0)
            .Should().NotContain("What the review of your last proposal found",
                "a clean review has nothing to show the master");
    }

    [Fact]
    public void RefusalNudge_AsksForAnAnswer_NotTheBlockAgain()
    {
        var nudge = new SpecDialogPromptFactory().BuildProposalRefusalNudge("ORIGINAL");

        nudge.Should().Contain("NOT shown").And.Contain("no ```yaml or ```outcome block")
            .And.Contain("A question is answered as it is asked")
            .And.EndWith("ORIGINAL").And.NotContain("schema validation");
    }

    private sealed record FutureOutcome : OutcomeProposal;

    [Fact]
    public void TurnResult_KindOf_AnOutcomeWithNoKind_Throws()
    {
        var act = () => SpecDialogTurnResult.KindOf(new FutureOutcome());

        act.Should().Throw<ArgumentOutOfRangeException>("a new outcome must not count as a discussion by default");
    }

    [Theory]
    [InlineData(typeof(AnswerOutcome), SpecDialogTurnKind.Answer)]
    [InlineData(typeof(PhaseOutcome), SpecDialogTurnKind.PhaseProposal)]
    [InlineData(typeof(BugOutcome), SpecDialogTurnKind.BugProposal)]
    [InlineData(typeof(EpicOutcome), SpecDialogTurnKind.EpicProposal)]
    public void TurnResult_KindOf_FollowsTheOutcome(Type outcomeType, SpecDialogTurnKind expected)
    {
        var draft = new PhaseDraft("p1", "g", "phase: p1", []);
        OutcomeProposal outcome = outcomeType.Name switch
        {
            nameof(PhaseOutcome) => new PhaseOutcome(draft),
            nameof(BugOutcome) => new BugOutcome(new BugTicketDraft("t", "d", null)),
            nameof(EpicOutcome) => new EpicOutcome(draft, [draft]),
            _ => new AnswerOutcome(),
        };

        SpecDialogTurnResult.KindOf(outcome).Should().Be(expected);
        SpecDialogTurnResult.On("slack", "r", outcome, SpecDialogTurnKind.Failure).Kind
            .Should().Be(SpecDialogTurnKind.Failure, "a turn that says what it is overrides its outcome");
    }

    [Fact]
    public void TranscriptTurn_RowWithoutKind_ReadsAsNull()
    {
        var turns = SpecDialogSessionMapper.ReadTranscript(
            """[{"role":"assistant","text":"old","at":"2026-09-01T00:00:00+00:00"}]""");
        var written = SpecDialogSessionMapper.WriteTranscript(
            [turns[0] with { Kind = SpecDialogTurnKind.Filing }]);

        turns.Should().ContainSingle().Which.Kind.Should().BeNull();
        SpecDialogSessionMapper.ReadTranscript(written)[0].Kind.Should().Be(SpecDialogTurnKind.Filing);
    }

    [Fact]
    public void TranscriptTurn_RowWithAnUnknownKind_ReadsAsNull()
    {
        var turns = SpecDialogSessionMapper.ReadTranscript(
            """[{"role":"assistant","text":"new","at":"2026-09-01T00:00:00+00:00","kind":"decision"},"""
            + """{"role":"assistant","text":"known","at":"2026-09-01T00:00:00+00:00","kind":"answer"}]""");

        turns.Should().HaveCount(2, "one unknown kind must not make the transcript unreadable");
        turns[0].Kind.Should().BeNull();
        turns[1].Kind.Should().Be(SpecDialogTurnKind.Answer);
    }
}
