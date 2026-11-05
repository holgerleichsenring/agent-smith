namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// The thread a design conversation's last turn ended with — the messages its master was last sent and answered,
/// masked — keyed on the conversation's SESSION id and read only when the next turn runs, so a table of its own.
/// <para>
/// One row is current; a write adds before it deletes the older rows by id, and a read takes the newest by id, so
/// two replicas racing lose the older turn's tool results and raise no error.
/// </para>
/// </summary>
public sealed class SpecDialogThread : EntityBase
{
    public long Id { get; set; }

    /// <summary>The conversation's session id — the read, the replace and the delete sweep by this.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The ticket steward led the turn; a turn of the other master never replays this thread.</summary>
    public bool Stewarded { get; set; }

    /// <summary>The messages, as the dialog thread codec writes them.</summary>
    public string MessagesJson { get; set; } = string.Empty;

    /// <summary>The transcript's length when the turn began: the turns from here on were added while or since it ran.</summary>
    public int TranscriptFrom { get; set; }

    /// <summary>The index of the kept reply when it is the text the master answered with, which it has already seen;
    /// null when the kept reply was substituted.</summary>
    public int? ReplyAt { get; set; }

    public DateTimeOffset WrittenAt { get; set; }
}
