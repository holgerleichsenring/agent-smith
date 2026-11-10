using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ec: the design prompt's contract, and a kept turn records its kind. 2026-11-09-0620b: one contract for
/// every turn, with no proposal ban.
/// </summary>
public sealed class SpecDialogDiscussionPromptTests
{
    private static PipelineContext With(params SpecDialogTurn[] transcript)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, transcript);
        return pipeline;
    }

    // 2026-11-09-0620b/k: no proposal ban, and no contract restating the skill — the turn's last words are the work
    // picture fact, and the filed-work clause when the conversation filed.
    [Fact]
    public void SpecDialogPrompt_FirstTurn_StatesNoProposalBan()
    {
        var prompt = new SpecDialogPromptFactory().Build(With(new SpecDialogTurn("user", "update everything")), 0, 0);

        prompt.Should().NotContain("MAY NOT propose").And.NotContain("Ready to propose")
            .And.NotContain("Propose the work once");
        prompt.TrimEnd().Should().EndWith("No work picture is stored yet.");
    }

    // 2026-11-09-0620b: the texts changed, not their places — the cached prefix and the reading of the prompt stay.
    [Fact]
    public void SpecDialogPrompt_SectionOrder_IsUnchanged()
    {
        var prompt = new SpecDialogPromptFactory().Build(With(new SpecDialogTurn("user", "update everything")), 0, 0);

        string[] order =
        [
            "## Design conversation", "[user", "## Your reply", "Write the reply text now",
            "No work picture is stored yet",
        ];
        var positions = order.Select(marker => prompt.IndexOf(marker, StringComparison.Ordinal)).ToList();
        positions.Should().OnlyContain(p => p >= 0).And.BeInAscendingOrder();
    }

    // 2026-09-17-042ed: the confirmation the findings were shown in is posted to the thread and
    // never appended to the transcript, so the edit turn is shown them here or nowhere.
    [Fact]
    public void EditTurn_AfterAReviewedProposal_IsShownItsFindings()
    {
        var pipeline = With(new SpecDialogTurn("user", "cut it in two"));
        pipeline.Set<OutcomeProposal>(ContextKeys.SpecDialogRevisedProposal,
            new SpecOutcome(new SpecDraft("p9999", "widget goal", "spec: p9999", [])) with
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
            new SpecOutcome(new SpecDraft("p9999", "widget goal", "spec: p9999", [])));

        new SpecDialogPromptFactory().Build(With(new SpecDialogTurn("user", "and now the next one")), 0, 0)
            .Should().NotContain("the review of your last proposal",
                "only an edit turn is handed a proposal, and only the router knows a turn is one");
        new SpecDialogPromptFactory().Build(reviewedClean, 0, 0)
            .Should().NotContain("What the review of your last proposal found",
                "a clean review has nothing to show the master");
    }

    [Fact]
    public void TurnResult_KindOf_AnOutcomeWithNoKind_Throws()
    {
        var act = () => SpecDialogTurnResult.KindOf(new FutureOutcome());

        act.Should().Throw<ArgumentOutOfRangeException>("a new outcome must not count as a discussion by default");
    }

    [Theory]
    [InlineData(typeof(AnswerOutcome), SpecDialogTurnKind.Answer)]
    [InlineData(typeof(SpecOutcome), SpecDialogTurnKind.SpecProposal)]
    [InlineData(typeof(BugOutcome), SpecDialogTurnKind.BugProposal)]
    [InlineData(typeof(EpicOutcome), SpecDialogTurnKind.EpicProposal)]
    public void TurnResult_KindOf_FollowsTheOutcome(Type outcomeType, SpecDialogTurnKind expected)
    {
        var draft = new SpecDraft("p1", "g", "spec: p1", []);
        OutcomeProposal outcome = outcomeType.Name switch
        {
            nameof(SpecOutcome) => new SpecOutcome(draft),
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

    private sealed record FutureOutcome : OutcomeProposal;
}
