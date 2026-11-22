namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>2026-10-13-9a59g: the kinds of a <see cref="RunConversationEntry"/>.</summary>
public static class RunConversationKinds
{
    public const string TicketText = "ticket-text";
    public const string Question = "question";
    public const string HandBack = "hand-back";
    public const string CutReview = "cut-review";

    /// <summary>2026-11-22-e812: the ticket text the run wrote into the tracker from the approved specs.</summary>
    public const string TicketRewritten = "ticket-rewritten";
}
