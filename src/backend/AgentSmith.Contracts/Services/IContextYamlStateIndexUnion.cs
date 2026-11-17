namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-17-023fb: resolves a context.yaml that two branches each extended with their own state.done index lines —
/// the merge conflict every pair of parallel specs produces, since both insert newest first under done.
/// </summary>
public interface IContextYamlStateIndexUnion
{
    /// <summary>
    /// The union of <paramref name="ours"/> and <paramref name="theirs"/> from their merge base (null: the file was
    /// added on both sides), or null when the conflict is more than added index lines: the two sides differ outside
    /// state.done in a way neither side's base explains, an entry the base had was changed or removed, or one key was
    /// added on both sides with different text. Null for a side that deleted the file.
    /// </summary>
    string? Resolve(string? @base, string? ours, string? theirs);
}
