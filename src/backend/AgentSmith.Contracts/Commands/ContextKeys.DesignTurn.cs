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

    /// <summary>2026-11-12-cd4cl: <c>TranscriptWindowConfig</c> — the project agent's transcript window.</summary>
    public const string SpecDialogTranscriptWindow = "SpecDialogTranscriptWindow";

    /// <summary>2026-11-12-92e9b: <c>ProposalReviewLedger</c> — what each review pass of this turn found, keyed by the
    /// exact text it reviewed; created by the first pass that records.</summary>
    public const string ProposalReviewLedger = "ProposalReviewLedger";
}
