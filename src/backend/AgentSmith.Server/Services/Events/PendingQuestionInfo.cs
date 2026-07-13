using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0327: dashboard contract for a waiting_for_input run's pending question —
/// what to render and everything the answer POST needs (the question id rides
/// server-side via the checkpoint row; the client only sends the answer text).
/// </summary>
public sealed record PendingQuestionInfo(
    string QuestionId,
    string Type,
    string Text,
    string? Context,
    IReadOnlyList<string> Choices,
    string? DefaultAnswer,
    DateTimeOffset AskedAt,
    DateTimeOffset AnswerDeadlineAt)
{
    public static PendingQuestionInfo? FromCheckpoint(RunCheckpointRecord? checkpoint)
    {
        if (checkpoint is null || checkpoint.ResumedAt is not null) return null;
        var question = JsonSerializer.Deserialize<DialogQuestion>(checkpoint.QuestionJson);
        if (question is null) return null;
        return new PendingQuestionInfo(
            question.QuestionId,
            question.Type.ToString(),
            question.Text,
            question.Context,
            question.Choices?.Select(c => c.Label).ToList() ?? [],
            question.DefaultAnswer,
            checkpoint.AskedAt,
            checkpoint.AnswerDeadlineAt);
    }
}
