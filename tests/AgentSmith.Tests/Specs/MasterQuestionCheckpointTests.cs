using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0453: a mid-run question is answerable where it is shown.
/// <para>
/// Two ask paths existed and only one was answerable. p0327's gate writes a checkpoint, so
/// the dashboard renders the question and the operator answers it in place. p0315d's mid-run
/// master question posted a ticket comment, parked the ticket and ended the run — no
/// checkpoint, so the card read "Question unavailable — open the run to answer", where there
/// was equally nothing to answer.
/// </para>
/// <para>
/// Live run 216b sat on exactly that: the answer was on the ticket at 10:55 and nothing
/// happened, because a parked ticket is deliberately outside the trigger statuses. The
/// operator's only way back in was a manual status move on the board.
/// </para>
/// </summary>
public sealed class MasterQuestionCheckpointTests
{
    [Fact]
    public void OneQuestionWithOptions_ReachesTheOperatorAsAChoice()
    {
        var composed = MasterQuestionCheckpoint.Compose(
            [new PlanOpenQuestion("q1", "May I raise the shared package?", ["yes", "no"])]);

        composed.Type.Should().Be(QuestionType.Choice);
        composed.Text.Should().Be("May I raise the shared package?");
        composed.Choices.Should().HaveCount(2);
        composed.QuestionId.Should().Be("q1", "the answer is matched back by the master's own id");
    }

    [Fact]
    public void AQuestionWithoutOptions_IsAnsweredInWords()
    {
        var composed = MasterQuestionCheckpoint.Compose(
            [new PlanOpenQuestion("q1", "Which transport is authoritative?", [])]);

        composed.Type.Should().Be(QuestionType.FreeText);
        composed.Choices.Should().BeNull();
    }

    /// <summary>
    /// The ticket comment asks for one reply covering every question ("Q1: …"), so the
    /// dashboard must present them the same way — two prompts for one reply would be a
    /// second answering convention nobody asked for.
    /// </summary>
    [Fact]
    public void SeveralQuestions_ArePresentedAsTheOneReplyTheCommentAsksFor()
    {
        var composed = MasterQuestionCheckpoint.Compose([
            new PlanOpenQuestion("q1", "Raise the pins?", ["yes"]),
            new PlanOpenQuestion("q2", "Upgrade the tooling?", ["yes"]),
        ]);

        composed.Text.Should().Contain("Q1: Raise the pins?").And.Contain("Q2: Upgrade the tooling?");
        composed.Type.Should().Be(QuestionType.FreeText, "one reply answers them together");
    }

    /// <summary>
    /// A parked run holds nothing but its branch, so the question waits as long as the
    /// operator needs. A short deadline here would answer for them.
    /// </summary>
    [Fact]
    public void TheQuestionWaitsForTheOperator_NotForAClock()
    {
        MasterQuestionCheckpoint.Compose([new PlanOpenQuestion("q1", "Well?", [])])
            .Timeout.Should().BeGreaterThan(TimeSpan.FromDays(7));
        MasterQuestionCheckpoint.Compose([new PlanOpenQuestion("q1", "Well?", [])])
            .DefaultAnswer.Should().BeNull("nothing may answer a mid-run question on the operator's behalf");
    }
}
