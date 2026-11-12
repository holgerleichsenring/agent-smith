namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-11-12-cd4c: design-turn keys beside the spec-dialog block, which stands at its file-length limit.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>2026-11-12-cd4ck: <c>IConversationNotes</c> — the store read_notes and write_notes act on; absent
    /// where there is none.</summary>
    public const string SpecDialogNotesStore = "SpecDialogNotesStore";

    /// <summary>2026-11-12-cd4ck: <c>string</c> — the conversation's notes as the turn began; absent while none are
    /// written.</summary>
    public const string SpecDialogNotes = "SpecDialogNotes";
}
