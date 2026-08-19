using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0453: the step that posts a mid-run question also makes it answerable in place.
/// <para>
/// This is the case that must be able to go red: remove the checkpoint call from the handler
/// and the dashboard is back to "Question unavailable — open the run to answer", with the
/// only way back into the run being a manual status move on the board.
/// </para>
/// </summary>
public sealed class MidRunQuestionIsAnswerableTests
{
    [Fact]
    public async Task AMidRunQuestion_IsCheckpointedSoTheRunCanBeResumedInPlace()
    {
        var writer = new RecordingWriter();

        var result = await Handler(writer).ExecuteAsync(Context(asked: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a parked question is an incomplete run, not a failure");
        writer.Questions.Should().ContainSingle(
            "without a checkpoint the dashboard has nothing to render and nowhere to reply")
            .Which.Text.Should().Contain("May I raise the shared package?");
    }

    /// <summary>
    /// A run that asked nothing must not leave a checkpoint behind — the dashboard would
    /// show a question nobody asked, and the run would look parked when it is not.
    /// </summary>
    [Fact]
    public async Task ARunThatAskedNothing_LeavesNoQuestionBehind()
    {
        var writer = new RecordingWriter();

        await Handler(writer).ExecuteAsync(Context(asked: false), CancellationToken.None);

        writer.Questions.Should().BeEmpty();
    }

    private static MasterOpenQuestionsHandler Handler(RecordingWriter writer) =>
        new(new NoOpPoster(), new FixedParkStatus(),
            new MasterQuestionCheckpoint(writer, NullLogger<MasterQuestionCheckpoint>.Instance),
            NullLogger<MasterOpenQuestionsHandler>.Instance);

    private static MasterOpenQuestionsContext Context(bool asked)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "2026-08-19T10-28-32-216b");
        if (asked)
            pipeline.Set<IReadOnlyList<PlanOpenQuestion>>(
                ContextKeys.MasterOpenQuestions,
                [new PlanOpenQuestion("q1", "May I raise the shared package?", ["yes", "no"])]);
        return new MasterOpenQuestionsContext(
            new Ticket(new TicketId("19213"), "migrate", "do it", null, "Active", "test"),
            new TrackerConnection { Name = "tracker" },
            pipeline);
    }

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

    private sealed class NoOpPoster : IPlanOpenQuestionsPoster
    {
        public Task PostAsync(
            TrackerConnection ticketConfig, TicketId ticketId,
            IReadOnlyList<PlanOpenQuestion> questions, string? parkStatus, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class FixedParkStatus : IClarificationParkStatusResolver
    {
        public string? TryResolve(PipelineContext pipeline, TrackerConnection tracker) => "In Test";

        public string UnresolvedReason => "no clarification status configured";
    }
}
