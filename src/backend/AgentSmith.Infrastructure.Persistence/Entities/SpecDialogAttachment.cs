namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-09-20-3af8: one image an operator attached to a design conversation, keyed on the
/// conversation's SESSION id.
/// <para>
/// Keyed on the session and not on the dialog id, because a dialog id is a TAB: a resume moves
/// a conversation onto a new dialog id while keeping its session id, and an attachment keyed on
/// the tab would be orphaned by the next resume.
/// </para>
/// <para>
/// The bytes are held as base64 TEXT. There is no byte store in this estate — no persisted
/// entity carries bytes, a run's own attachments live inside a sandbox that is disposed with
/// it, and the durable artifact slot holds a nullable string — so this is the smallest thing
/// that keeps an operator's screenshot after the turn that carried it, not a byte store
/// pretending to be one.
/// </para>
/// </summary>
public sealed class SpecDialogAttachment : EntityBase
{
    public long Id { get; set; }

    /// <summary>The conversation's session id — the delete sweeps by this.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The kind the uploaded BYTES were read as, never what the client called them.</summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>Base64 of the image. Unbounded by design — see the configuration.</summary>
    public string ContentBase64 { get; set; } = string.Empty;
}
