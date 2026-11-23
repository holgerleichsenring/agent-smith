using System.Text.Json;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-23-31dch: who can answer rides with the question and its checkpoint; a question and a checkpoint stored
/// before the field existed read as the target's.</summary>
public sealed class QuestionAnswererStorageTests
{
    [Fact]
    public void Compose_CarriesEachAnswererOnce_InTheOrderAsked()
    {
        var composed = MasterQuestionCheckpoint.Compose(
        [
            new PlanOpenQuestion("1", "Which payload?", []) { Answerer = QuestionAnswerers.Plan },
            new PlanOpenQuestion("2", "Token scope?", []) { Answerer = QuestionAnswerers.Environment },
        ], "ask-1");

        composed.Answerers.Should().Equal(QuestionAnswerers.Plan, QuestionAnswerers.Environment);
        var stored = JsonSerializer.Deserialize<DialogQuestion>(JsonSerializer.Serialize(composed))!;
        stored.AnswerersOrTarget.Should().Equal(QuestionAnswerers.Plan, QuestionAnswerers.Environment);
    }

    [Fact]
    public void ACheckpointWrittenBeforeTheField_ResumesAsTarget()
    {
        const string old = """{"QuestionId":"ask-1","Type":1,"Text":"Which branch?","Context":null,"Choices":null,"DefaultAnswer":null,"Timeout":"14.00:00:00"}""";
        var question = JsonSerializer.Deserialize<DialogQuestion>(old)!;
        var asked = JsonSerializer.Deserialize<PlanOpenQuestion>("""{"Id":"1","Question":"Which branch?","Options":[]}""")!;
        var checkpoint = new RunCheckpointRecord("r-1", "alpha", "42", null, "p", "job-1", "ask-1", old, "[]", "{}", 1,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(14), null);

        question.Answerers.Should().BeNull();
        question.AnswerersOrTarget.Should().Equal(QuestionAnswerers.Target);
        asked.Answerer.Should().Be(QuestionAnswerers.Target);
        PendingQuestionInfo.FromCheckpoint(checkpoint)!.Answerers.Should().Equal(QuestionAnswerers.Target);
    }

    [Fact]
    public void QuestionAnswerers_Parse_ReadsTheThreeValuesOnly()
    {
        QuestionAnswerers.Parse("PLAN ").Should().Be(QuestionAnswerers.Plan);
        QuestionAnswerers.Parse(null).Should().BeNull();
        QuestionAnswerers.Parse("admin").Should().BeNull();
        QuestionAnswerers.Refusal(null).Should().Contain("needs 'answerer'");
    }
}
