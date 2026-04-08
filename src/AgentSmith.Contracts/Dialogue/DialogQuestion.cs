namespace AgentSmith.Contracts.Dialogue;

public sealed record DialogQuestion(
    string QuestionId,
    QuestionType Type,
    string Text,
    string? Context,
    IReadOnlyList<string>? Choices,
    string? DefaultAnswer,
    TimeSpan Timeout);
