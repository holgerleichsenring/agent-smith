namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>2026-11-04-96a0a: what admission made of a conversation name — the line to store, or the rule it broke.</summary>
public sealed record ConversationSubjectVerdict(string? Subject, string? Refusal)
{
    public static ConversationSubjectVerdict Admitted(string subject) => new(subject, null);

    public static ConversationSubjectVerdict Refused(string rule) => new(null, rule);
}
