using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

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
    /// <summary>
    /// 2026-09-17-042ej: a stored question that cannot be deserialized is ABSENT, not an
    /// exception. One malformed row used to take down whichever read joined it — and on the
    /// filed-work read that is every ticket of a conversation, over one parked run.
    /// </summary>
    public static PendingQuestionInfo? FromCheckpoint(
        RunCheckpointRecord? checkpoint, ILogger? logger = null)
    {
        if (checkpoint is null || checkpoint.ResumedAt is not null) return null;
        DialogQuestion? question;
        try
        {
            question = JsonSerializer.Deserialize<DialogQuestion>(checkpoint.QuestionJson);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            logger?.LogWarning(
                ex, "The parked question of run {RunId} is unreadable — shown as absent",
                checkpoint.RunId);
            return null;
        }
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
