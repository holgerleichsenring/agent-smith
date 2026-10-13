namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-50c7: what one bootstrap round's context owns — its directories now, those of them
/// it did not own when its baseline was last written, and every claim of the repository it knows
/// of (the earlier rounds' and its own), which decide the files it measures.
/// </summary>
public sealed record StructureBaselineOwned(
    string Context, IReadOnlyList<string> Directories, IReadOnlyList<string> NewlyOwned,
    IReadOnlyList<StructureClaim> Claims);
