using System.Text.Json.Serialization;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042eg: what a filed ticket became, once the approval had finished creating it.
/// Serialized by NAME on every path — the pane reads it, the session row keeps it, and a
/// number in either would be a state nobody could read back after a rename.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FiledStartState>))]
public enum FiledStartState
{
    /// <summary>A run will pick it up: the poller's own envelope resolves it to the filing
    /// project, and it sits in a status that triggers.</summary>
    Started,

    /// <summary>Nothing will pick it up yet, and <see cref="FiledWorkStart.Reason"/> says what
    /// is in the way — a resolution the ticket does not satisfy, a permission the approver does
    /// not hold, a status the tracker would not move it out of.</summary>
    NotStarted,

    /// <summary>
    /// A slice record: not work at all, so it was never resolved and never moved.
    /// <para>
    /// 2026-09-22-b3d7: NOTHING WRITES THIS ANY MORE — an approved cut files one ticket and no
    /// records. The member stays because the enum is serialized BY NAME onto the session row and
    /// every filing stored before this phase carries it: an unreadable filing row is shown as
    /// ABSENT rather than as an error, so deleting the member would not surface as a missing word
    /// but as the whole stored filing silently vanishing from the pane. The filed-work reader also
    /// still skips the run lookup for a row carrying it, which is the right answer for those rows.
    /// </para>
    /// </summary>
    Record,

    /// <summary>2026-09-22-9519: the conversation that filed it closed it again, and the TRACKER
    /// said the close landed. Written only on that answer: a close a tracker did not perform
    /// leaves the record exactly as it was, because a record saying withdrawn about a ticket still
    /// on the board is the failure this state exists to prevent.</summary>
    Withdrawn,
}

/// <summary>
/// The state one filed ticket ended in, with the reason in the words the operator reads. Nullable
/// on <see cref="FiledTicket"/>: a filing written before this phase deserializes without one and
/// reads as unknown rather than as a claim nobody made.
/// <para>
/// 2026-09-22-9519: the STATE is nullable for the same reason the whole record is. It is stored by
/// name and a stored filing that cannot be read is shown as ABSENT, so a server meeting a state
/// name a later build added would lose the WHOLE filing over one word. The store reads it through
/// <see cref="TolerantNullableEnumConverter{TEnum}"/>, which answers null for a name it cannot
/// place — one word lost, the tickets, the error and the notes all kept.
/// </para>
/// </summary>
public sealed record FiledWorkStart(FiledStartState? State, string Reason)
{
    /// <summary>
    /// The line a filing notice appends under the ticket it is about, as a NESTED BULLET: a bare
    /// newline inside a list item is a soft break in CommonMark, so on the page the reason would
    /// run on after the title, while Slack's mrkdwn would break it — one markup reads in both.
    /// <para>Ignored by the serializer: this record is stored on the session row and pushed to the
    /// page, and a rendering of two fields already there is not a third field.</para>
    /// </summary>
    [JsonIgnore]
    public string Note => $"\n  - {Label}: {Reason}";

    /// <summary>
    /// 2026-09-22-9519: EVERY state names itself. This used to fall through to "record" for
    /// anything it had not been taught, so the first state added after it would have been
    /// announced as a slice record on every channel the notice reaches. The catch-all is now the
    /// unreadable case alone — a state this build cannot name is unknown, never one of the words
    /// above.
    /// </summary>
    private string Label => State switch
    {
        FiledStartState.Started => "started",
        FiledStartState.NotStarted => "not started",
        FiledStartState.Record => "record",
        FiledStartState.Withdrawn => "withdrawn",
        _ => "unknown",
    };
}
