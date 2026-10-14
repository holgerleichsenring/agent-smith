namespace AgentSmith.Contracts.Commands;

public static partial class ContextKeys
{
    /// <summary>2026-10-13-8762m: this run's build notes not yet read back from a record
    /// (IReadOnlyDictionary&lt;string, <see cref="Specs.SpecBuildNote"/>&gt;, keyed by spec id), written by
    /// hand_forward and read by the record step and the next spec's prompts.</summary>
    public const string SpecBuildNotes = "SpecBuildNotes";
}
