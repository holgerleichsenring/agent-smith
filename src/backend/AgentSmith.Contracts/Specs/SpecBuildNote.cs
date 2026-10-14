namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-8762m: what a spec's master hands the next spec — what it renamed, moved, persisted
/// and deviated from. A renamed or moved entry reads <c>&lt;old&gt; -&gt; &lt;new&gt;</c>. Its only home is
/// the spec's record (<c>outcome.hand_forward</c>); until the record is written it waits in
/// <c>ContextKeys.SpecBuildNotes</c>.
/// </summary>
public sealed record SpecBuildNote(
    string SpecId,
    IReadOnlyList<string> Renamed,
    IReadOnlyList<string> Moved,
    IReadOnlyList<string> Persisted,
    IReadOnlyList<string> Deviations);
