namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-09-25-8e51c: the text of the ticket a design conversation belongs to, as the conversation
/// read it, keyed on the conversation's SESSION id.
/// <para>
/// A table of its own rather than a column on the session: the session row is read on every reply
/// — the page refetches its view after each hub message — and a ticket's text is read only when a
/// turn runs. Keyed on the session and not the dialog id, for the reason the attachments give: a
/// dialog id is a TAB, and a resume moves a conversation onto a new one.
/// </para>
/// <para>
/// <see cref="Fingerprint"/> is of the ticket as the TRACKER stored it, not of what is kept here:
/// it is how a later turn can say the ticket moved underneath the conversation without the
/// operator having to notice and ask.
/// </para>
/// </summary>
public sealed class SpecDialogTicketText : EntityBase
{
    public long Id { get; set; }

    /// <summary>The conversation's session id — the delete sweeps by this.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The tracker's own id for the ticket — what a re-read addresses it with.</summary>
    public string TicketId { get; set; } = string.Empty;

    /// <summary>The ticket's title, as read.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Title, body and the comments that are not ours, already stripped and capped.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>True when the cap dropped part of the ticket, so a turn can say so.</summary>
    public bool Truncated { get; set; }

    /// <summary>Of the ticket as the tracker stored it, to detect that it has moved since.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>When the conversation last read it.</summary>
    public DateTimeOffset ReadAt { get; set; }
}
