namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>2026-10-13-9a59f: the run-wide ordinal of the last master pass opened (int). Carried by
    /// the checkpoint, so a resumed master continues the numbering.</summary>
    public const string MasterPassOrdinal = "MasterPassOrdinal";

    /// <summary>2026-10-13-9a59f: true when the last master pass ended on a question (bool). Set only by
    /// the pass recorder, cleared when the next pass opens — that pass opened after an answer.</summary>
    public const string MasterPassEndedOnQuestion = "MasterPassEndedOnQuestion";
}
