using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-03-3c07: the resumed MasterOpenQuestions step hands the delivered answer to the
/// master that asked instead of parking on the answered question again.
/// <para>
/// The cursor of a master park starts with the asking step, so a resume re-enters here with
/// the question restored from the checkpoint and the answer staged by the resume reader.
/// Live run a109 showed the two ways this went wrong: the restored park marker parked the run
/// at its first command, and nothing on this path read the answer at all.
/// </para>
/// </summary>
public sealed class MasterQuestionResumeTests
{
    private const string AskId = "ask-7f3a";
    private const string Question = "Should the refresh window be 5 or 15 minutes?";

    [Fact]
    public async Task ResumedAnswer_ReengagesTheMasterInsteadOfParkingAgain()
    {
        var writer = new RecordingWriter();
        var poster = new RecordingPoster();
        var context = Resumed(delivered: new DialogAnswer(
            AskId, "15 minutes", null, DateTimeOffset.UtcNow, "dashboard-operator"), phaseId: "p0001");

        var result = await Handler(writer, poster).ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.InsertNext.Should().NotBeNull("the master must be re-engaged with the answer")
            .And.HaveCount(2);
        result.InsertNext![0].Name.Should().Be(CommandNames.AgenticMaster);
        result.InsertNext[1].Name.Should().Be(CommandNames.MasterOpenQuestions,
            "a second question of the re-engaged master must park the same way");
        result.InsertNext.Should().OnlyContain(c => c.PhaseId == "p0001",
            "the re-engaged master belongs to the phase whose question it answers");
        writer.Questions.Should().BeEmpty("an answered question is not checkpointed again");
        poster.Posted.Should().Be(0, "an answered question is not re-posted to the ticket");
    }

    [Fact]
    public async Task ResumedAnswer_IsInTheMastersInputAndTheParkIsCleared()
    {
        var context = Resumed(delivered: new DialogAnswer(
            AskId, "15 minutes", "the session must survive one refresh", DateTimeOffset.UtcNow, "dashboard-operator"));
        // An older checkpoint captured the marker; the restored run must not carry it on.
        context.Pipeline.Set(ContextKeys.OpenQuestionsAwaitingAnswer, true);

        await Handler(new RecordingWriter(), new RecordingPoster()).ExecuteAsync(context, CancellationToken.None);

        context.Pipeline.Get<Dictionary<string, string>>(ContextKeys.PlanAnswers)
            .Should().ContainKey("1").WhoseValue.Should().Be("15 minutes (the session must survive one refresh)",
                "the phase prompt renders PlanAnswers as the operator's answers — the seam that already exists");
        context.Pipeline.Has(ContextKeys.MasterOpenQuestions).Should().BeFalse("the question is answered");
        context.Pipeline.Has(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeFalse("the run is no longer parked");
        context.Pipeline.Has(ContextKeys.ResumedDialogueAnswer).Should().BeFalse("the answer is consumed exactly once");
    }

    [Fact]
    public async Task ResumedAnswer_IsRecordedOnTheDialogueTrailLikeTheGateDoes()
    {
        var trail = new Mock<IDialogueTrail>();
        var intake = new MasterAnswerIntake(trail.Object, NullLogger<MasterAnswerIntake>.Instance);
        var context = Resumed(delivered: new DialogAnswer(
            AskId, "15 minutes", null, DateTimeOffset.UtcNow, "dashboard-operator"));

        await Handler(new RecordingWriter(), new RecordingPoster(), intake)
            .ExecuteAsync(context, CancellationToken.None);

        // The ask is composed afresh (a508: matched by text, never by an id minted to fit),
        // so the trail pairs the question TEXT with the answer under one shared id.
        trail.Verify(t => t.RecordAsync(
            It.Is<DialogQuestion>(q => q.Text == Question),
            It.Is<DialogAnswer>(a => a.Answer == "15 minutes")), Times.Once);
    }

    /// <summary>
    /// 2026-08-25-a508's rule holds here too: a delivered answer belongs to the ask the run
    /// parked on. A question that is neither that ask's id nor its text is a new ask and parks.
    /// </summary>
    [Fact]
    public async Task AnAnswerToAnotherAsk_IsNotTakenForThisOne_TheQuestionParks()
    {
        var writer = new RecordingWriter();
        var poster = new RecordingPoster();
        var context = Resumed(delivered: new DialogAnswer(
            "ask-other", "yes", null, DateTimeOffset.UtcNow, "dashboard-operator"));
        context.Pipeline.Set(ContextKeys.DialogueQuestion, new DialogQuestion(
            "ask-other", QuestionType.FreeText, "May I raise the shared package?",
            null, null, null, TimeSpan.FromDays(14)));

        var result = await Handler(writer, poster).ExecuteAsync(context, CancellationToken.None);

        result.InsertNext.Should().BeNull("no master is re-engaged on an answer to a different ask");
        result.Message.Should().Contain("awaiting_user_input");
        writer.Questions.Should().ContainSingle("the new ask is checkpointed as its own slot");
        poster.Posted.Should().Be(1);
        context.Pipeline.Get<bool>(ContextKeys.OpenQuestionsAwaitingAnswer).Should().BeTrue();
    }

    // ---- fixtures ----

    // The shape ResumeRequestReader leaves behind: the question restored from the checkpoint,
    // the parked ask staged, the operator's answer delivered.
    private static MasterOpenQuestionsContext Resumed(DialogAnswer delivered, string? phaseId = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "2026-09-08T13-28-32-a109");
        pipeline.Set<IReadOnlyList<PlanOpenQuestion>>(
            ContextKeys.MasterOpenQuestions, [new PlanOpenQuestion("1", Question, ["5", "15"])]);
        pipeline.Set(ContextKeys.DialogueQuestion,
            MasterQuestionCheckpoint.Compose([new PlanOpenQuestion("1", Question, ["5", "15"])], AskId));
        pipeline.Set(ContextKeys.ResumedDialogueAnswer, delivered);
        return new MasterOpenQuestionsContext(
            new Ticket(new TicketId("42"), "refresh", "the session drops", null, "Active", "test"),
            new TrackerConnection { Name = "tracker" },
            pipeline,
            new PipelineCommand(CommandNames.MasterOpenQuestions) { PhaseId = phaseId });
    }

    private static MasterOpenQuestionsHandler Handler(
        RecordingWriter writer, RecordingPoster poster, IMasterAnswerIntake? intake = null) =>
        new(poster, new FixedParkStatus(),
            new MasterQuestionCheckpoint(
                writer, new DialogueJobIdentity(new Mock<IProgressReporter>().Object),
                NullLogger<MasterQuestionCheckpoint>.Instance),
            intake ?? new MasterAnswerIntake(Mock.Of<IDialogueTrail>(), NullLogger<MasterAnswerIntake>.Instance),
            NullLogger<MasterOpenQuestionsHandler>.Instance);

    private sealed class RecordingWriter : IDialogueCheckpointWriter
    {
        public List<DialogQuestion> Questions { get; } = [];

        public Task<bool> TryCheckpointAsync(
            PipelineContext pipeline, DialogQuestion question, string dialogueJobId, CancellationToken ct)
        {
            Questions.Add(question);
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingPoster : IPlanOpenQuestionsPoster
    {
        public int Posted { get; private set; }

        public Task PostAsync(
            PipelineContext pipeline, TrackerConnection ticketConfig, Ticket ticket,
            IReadOnlyList<PlanOpenQuestion> questions, string? parkStatus, CancellationToken ct)
        {
            Posted++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedParkStatus : IClarificationParkStatusResolver
    {
        public string? TryResolve(PipelineContext pipeline, TrackerConnection tracker) => "Question";

        public string UnresolvedReason => "no clarification status configured";
    }
}
