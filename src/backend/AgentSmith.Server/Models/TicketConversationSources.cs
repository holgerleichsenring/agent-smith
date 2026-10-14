namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59h: where a conversation entry came from.</summary>
public static class TicketConversationSources
{
    public const string Ticket = "ticket";
    public const string Question = "question";
    public const string PrReview = "pr-review";
    public const string SpecCut = "spec-cut";
    public const string Conversation = "conversation";
}

/// <summary>2026-10-13-9a59h: who spoke — a role, never an author where none is recorded.</summary>
public static class TicketConversationRoles
{
    public const string Person = "person";
    public const string Agent = "agent";
    public const string System = "system";
    public const string Reviewer = "reviewer";
}

/// <summary>2026-10-13-9a59h: the groups an entry outside every attempt falls into.</summary>
public static class TicketConversationGroups
{
    public const string Before = "before";
    public const string Now = "now";
}

/// <summary>
/// 2026-10-13-9a59h: the kinds the read names itself; a hand-back, a question and a cut review keep their
/// recorded kind, a pull-request entry its record's.
/// </summary>
public static class TicketConversationKinds
{
    public const string Edited = "edited";
    public const string Answer = "answer";
    public const string DefaultAnswer = "default-answer";
    public const string CommandAnswer = "command-answer";
    public const string Comment = "comment";
    public const string Turn = "turn";
}
