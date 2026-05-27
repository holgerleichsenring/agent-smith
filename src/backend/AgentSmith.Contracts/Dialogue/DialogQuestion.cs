namespace AgentSmith.Contracts.Dialogue;

public sealed record DialogQuestion(
    string QuestionId,
    QuestionType Type,
    string Text,
    string? Context,
    IReadOnlyList<DialogChoice>? Choices,
    string? DefaultAnswer,
    TimeSpan Timeout,
    int? RecommendedIndex = null);
