namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-04-96a0a: how long a conversation's subject may be. In Contracts because two layers need the same number:
/// the admission rule name_conversation runs, in Application, and the column that stores what it admitted.
/// </summary>
public static class ConversationSubjectLimits
{
    public const int Length = 120;
}
