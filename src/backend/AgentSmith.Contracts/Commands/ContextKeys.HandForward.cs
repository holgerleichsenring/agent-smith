namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>2026-10-13-8762m: this run's build notes not yet read back from a record
    /// (IReadOnlyDictionary&lt;string, <see cref="Specs.SpecBuildNote"/>&gt;, keyed by spec id), written by
    /// SpecHandForward from the report's read (2026-11-22-96c8a; was the hand_forward tool) and read by the record
    /// step and the next spec's prompts.</summary>
    public const string SpecBuildNotes = "SpecBuildNotes";

    /// <summary>2026-10-25-9324c: the memories each spec of this run relied on (IReadOnlyDictionary&lt;string,
    /// IReadOnlyList&lt;<see cref="Memory.MemoryCitation"/>&gt;&gt;, keyed by spec id), written from the relied_on the
    /// report's read names (2026-11-22-96c8a: the union over the spec's passes) and read once at run end, when each
    /// spec's final standing becomes their evidence.</summary>
    public const string MemoryReliance = "MemoryReliance";

    /// <summary>2026-11-22-96c8a: the follow-ups each spec's master proposed over all its passes
    /// (IReadOnlyDictionary&lt;string, IReadOnlyList&lt;MasterFollowUp&gt;&gt;, keyed by spec id, one per goal ignoring
    /// case), appended after each report read and read by the spec's account.</summary>
    public const string SpecFollowUps = "SpecFollowUps";

    /// <summary>2026-11-22-96c8a: the ticket instructions the run's masters refused (IReadOnlyList&lt;IgnoredInstruction&gt;,
    /// one per quote), across every spec and pass; a failed read never clears it. Read by WriteRunResult.</summary>
    public const string RunIgnoredInstructions = "RunIgnoredInstructions";
}
