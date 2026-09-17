using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ec: a turn may propose once an assistant turn recorded as an answer is
/// followed, anywhere later, by an operator message — read from the recorded kind, never
/// from the text.
/// </summary>
public sealed class SpecDialogProposalRuleTests
{
    private static SpecDialogTurn User(string text = "operator") => new(SpecDialogTurn.UserRole, text);

    private static SpecDialogTurn Assistant(SpecDialogTurnKind? kind, string text = "reply") =>
        new(SpecDialogTurn.AssistantRole, text, kind);

    [Fact]
    public void ProposalRule_FirstTurn_MayNotPropose() =>
        SpecDialogProposalRule.MayPropose([User("update all dependencies")]).Should().BeFalse();

    [Fact]
    public void ProposalRule_AfterAnAnswerAndAnOperatorReply_MayPropose() =>
        SpecDialogProposalRule.MayPropose([User(), Assistant(SpecDialogTurnKind.Answer), User()])
            .Should().BeTrue();

    [Fact]
    public void ProposalRule_AnAnswerNotYetRepliedTo_MayNotPropose() =>
        SpecDialogProposalRule.MayPropose([User(), Assistant(SpecDialogTurnKind.Answer)])
            .Should().BeFalse();

    [Theory]
    [InlineData(SpecDialogTurnKind.Failure)]
    [InlineData(SpecDialogTurnKind.Notice)]
    [InlineData(SpecDialogTurnKind.Filing)]
    [InlineData(SpecDialogTurnKind.PhaseProposal)]
    public void ProposalRule_FailedTurnOrNotice_DoesNotCountAsDiscussion(SpecDialogTurnKind kind) =>
        SpecDialogProposalRule.MayPropose([User(), Assistant(kind), User()]).Should().BeFalse();

    [Fact]
    public void ProposalRule_AnswerQuotingYaml_CountsAsDiscussion() =>
        SpecDialogProposalRule.MayPropose(
            [User(), Assistant(SpecDialogTurnKind.Answer, "The file reads:\n```yaml\nphase: p1\n```"), User()])
            .Should().BeTrue();

    [Fact]
    public void ProposalRule_EditNoteAfterAProposal_MayPropose() =>
        SpecDialogProposalRule.MayPropose(
            [User(), Assistant(SpecDialogTurnKind.Answer), User(),
                Assistant(SpecDialogTurnKind.PhaseProposal), User("split it in two")])
            .Should().BeTrue();

    [Fact]
    public void ProposalRule_LegacyTurnWithoutKind_CountsAsDiscussion() =>
        SpecDialogProposalRule.MayPropose([User(), Assistant(null), User()]).Should().BeTrue();

    [Fact]
    public void ProposalRule_FromThePipeline_ReadsTheSeededTranscript()
    {
        var pipeline = new AgentSmith.Contracts.Commands.PipelineContext();
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(
            AgentSmith.Contracts.Commands.ContextKeys.SpecDialogTranscript,
            [User(), Assistant(SpecDialogTurnKind.Answer), User()]);

        SpecDialogProposalRule.MayPropose(pipeline).Should().BeTrue();
        SpecDialogProposalRule.MayPropose(new AgentSmith.Contracts.Commands.PipelineContext())
            .Should().BeFalse("a run with no transcript has held no discussion");
    }
}
