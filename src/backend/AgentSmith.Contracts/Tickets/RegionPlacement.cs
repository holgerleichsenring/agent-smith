namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-10-28-7a69b: what a region rewrite may do with a body that carries no complete marker pair.
/// </summary>
public enum RegionPlacement
{
    /// <summary>Replace the marked region; an unmarked body is refused, because the framework once
    /// rendered it whole and nothing says where its text ends.</summary>
    ReplaceOnly = 0,

    /// <summary>Replace the marked region, or append one below an unmarked body a person wrote.</summary>
    ReplaceOrAppend = 1,
}
