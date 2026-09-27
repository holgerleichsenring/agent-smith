namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// A spec-dialog design session (p0315a). Lives in the relational
/// system-of-record — NOT in Redis — because this deployment's Redis is
/// volatile and a design transcript must survive a flush/restart. Keyed by
/// chat thread (Platform + ThreadId) so parallel threads stay isolated;
/// SessionId is the short handle used by "/spec resume &lt;id&gt;".
/// </summary>
public sealed class SpecDialogSession : EntityBase
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    /// <summary>Active scope: the project the session is grounded on.</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>JSON array of repo names within the active scope.</summary>
    public string ReposJson { get; set; } = "[]";

    /// <summary>JSON array of ordered user/assistant transcript turns.</summary>
    public string TranscriptJson { get; set; } = "[]";

    /// <summary>
    /// JSON of the CONFIRMED outcome proposal (p0315e) — null until the human
    /// approves one in-thread. p0315c files tickets from this handoff.
    /// </summary>
    public string? ConfirmedOutcomeJson { get; set; }

    /// <summary>
    /// JSON of the proposal under discussion — the latest one a turn put to the person, kept
    /// until a later one supersedes it or the person rejects it. What a reloaded page shows
    /// in the proposal pane; the transcript keeps the reply it came from.
    /// </summary>
    public string? LatestProposalJson { get; set; }

    /// <summary>JSON of what the latest filing attempt created, and its error if it stopped.</summary>
    public string? LatestFilingJson { get; set; }

    /// <summary>
    /// 2026-09-20-4b0af: what this conversation is ABOUT, in the conversation's own language —
    /// minted once from its opening exchange and never revised, so a heading cannot rename
    /// itself under a reader. Null on every conversation that existed before the mint did, and
    /// on every one whose mint was refused or failed; the heading then falls back to the first
    /// line the person wrote, which is what it always showed.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// 2026-09-25-8e51b: the tracker CONNECTION this conversation's ticket lives on, and the
    /// ticket in the spelling the approval record uses — the spec key, which lowercases the id and
    /// collapses every non-alphanumeric character. Both null for a conversation that belongs to no
    /// ticket, which is every conversation that existed before this.
    /// <para>
    /// The spelling matters: a raw id would make DPG-1239 and dpg-1239 two conversations on a
    /// case-sensitive database and ONE approval record — two drafts of the artifact this pair
    /// exists to keep single — and one conversation on a case-insensitive one.
    /// </para>
    /// </summary>
    public string? Tracker { get; set; }

    /// <inheritdoc cref="Tracker"/>
    public string? TicketKey { get; set; }

    /// <summary>False once the session is closed or forked away from.</summary>
    public bool IsOpen { get; set; } = true;

    public DateTimeOffset LastActivityAt { get; set; }
}
