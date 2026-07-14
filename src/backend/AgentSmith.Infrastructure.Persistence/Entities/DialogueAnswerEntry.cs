namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0327: one durable inbox row — an operator's answer to a dialogue question,
/// persisted BEFORE the hot stream so no answer is lost between checkpoint and
/// resume (or across a server restart). UNIQUE(DialogueJobId, QuestionId) makes
/// "first answer wins" a database guarantee.
/// </summary>
public sealed class DialogueAnswerEntry : EntityBase
{
    public long Id { get; set; }
    public string DialogueJobId { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string AnsweredBy { get; set; } = string.Empty;
    public DateTimeOffset AnsweredAt { get; set; }
}
