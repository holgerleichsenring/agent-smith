namespace AgentSmith.Contracts.Dialogue;

public sealed record DialogAnswer(
    string QuestionId,
    string Answer,
    string? Comment,
    DateTimeOffset AnsweredAt,
    string AnsweredBy);
