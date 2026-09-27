namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// Column-length limits shared by the entity configurations and the code that has to
/// respect them. An indexed string column MUST cap at 191 chars or the MySQL utf8mb4
/// index-key-length limit (767 bytes / 4 bytes-per-char) fails the migration.
/// </summary>
public static class PersistenceLimits
{
    public const int IndexedString = 191;

    /// <summary>
    /// 2026-09-20-4b0af: a spec-dialog conversation's minted subject. Not indexed — capped
    /// because a heading has a width, and because a bounded string is what lets the column
    /// be declared by length alone, leaving the type to each provider's own convention.
    /// The admission rule that decides whether a minted answer is stored reads this, so the
    /// heading and the column can never disagree about how long a subject may be.
    /// </summary>
    public const int ConversationSubject = 120;

    /// <summary>2026-09-25-8e51c: the column holds exactly what the composer may produce — the
    /// number is declared once, in Contracts, so neither side can pick its own.</summary>
    public const int SeededTicketText = global::AgentSmith.Contracts.Tickets.SeededTicketLimits.Text;
}
