using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Dialogue;

public sealed record DialogQuestion(
    string QuestionId,
    QuestionType Type,
    string Text,
    string? Context,
    IReadOnlyList<DialogChoice>? Choices,
    string? DefaultAnswer,
    TimeSpan Timeout,
    int? RecommendedIndex = null)
{
    /// <summary>2026-11-23-31dch: who can answer the questions this one carries (<see cref="AgentSmith.Domain.Models.QuestionAnswerers"/>),
    /// distinct, in the order asked; null on a question stored before the field existed, which is the target's.</summary>
    public IReadOnlyList<string>? Answerers { get; init; }

    /// <summary>The answerers, the target's when none was stored.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> AnswerersOrTarget =>
        Answerers is { Count: > 0 } named ? named : [AgentSmith.Domain.Models.QuestionAnswerers.Target];
}
