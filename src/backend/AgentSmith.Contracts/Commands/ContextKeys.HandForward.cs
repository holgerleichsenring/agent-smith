namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>2026-10-13-8762m: this run's build notes not yet read back from a record
    /// (IReadOnlyDictionary&lt;string, <see cref="Specs.SpecBuildNote"/>&gt;, keyed by spec id), written by
    /// hand_forward and read by the record step and the next spec's prompts.</summary>
    public const string SpecBuildNotes = "SpecBuildNotes";

    /// <summary>2026-10-25-9324c: the memories each spec of this run relied on (IReadOnlyDictionary&lt;string,
    /// IReadOnlyList&lt;<see cref="Memory.MemoryCitation"/>&gt;&gt;, keyed by spec id), written by hand_forward's
    /// relied_on and read once at run end, when each spec's final standing becomes their evidence.</summary>
    public const string MemoryReliance = "MemoryReliance";
}
