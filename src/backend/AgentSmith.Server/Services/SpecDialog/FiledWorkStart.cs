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
}

/// <summary>
/// The state one filed ticket ended in, with the reason in the words the operator reads. Nullable
/// on <see cref="FiledTicket"/>: a filing written before this phase deserializes without one and
/// reads as unknown rather than as a claim nobody made.
/// </summary>
public sealed record FiledWorkStart(FiledStartState State, string Reason)
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

    private string Label => State switch
    {
        FiledStartState.Started => "started",
        FiledStartState.NotStarted => "not started",
        _ => "record",
    };
}
