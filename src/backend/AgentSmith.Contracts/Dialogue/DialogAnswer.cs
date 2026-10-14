namespace AgentSmith.Contracts.Dialogue;

public sealed record DialogAnswer(
    string QuestionId,
    string Answer,
    string? Comment,
    DateTimeOffset AnsweredAt,
    string AnsweredBy)
{
    /// <summary>The comment an answer read off the parked run's ticket is delivered with.</summary>
    public const string FromTicketComment = "ticket-comment";
}
