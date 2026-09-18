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

    /// <summary>A slice record. It is not work at all, so it is never resolved and never
    /// moved — the state is read off the label the filer used, not asked of the resolver.</summary>
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
