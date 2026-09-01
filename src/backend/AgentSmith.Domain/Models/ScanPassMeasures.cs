namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-09-01-3653: what a scan's master pass was GIVEN and how far it got.
/// <para>
/// Every number in the argument this record ends had been a guess, including the ones in
/// its own first draft: the scan prompt was called fifty-two thousand characters, the
/// master skill seven and a half thousand with no placeholders when it carries three
/// reference tokens the body resolver inlines. Two wrong numbers in one argument, from the
/// same absence — nothing wrote it down.
/// </para>
/// <para>
/// <see cref="TurnsUsed"/> is NEAR-EXACT. The pinned SDK exposes no iteration count on its
/// response; what the response carries is every message of the pass, so assistant messages
/// are the honest handle, and a provider that splits one turn across messages counts high.
/// It must be read and rendered as that, never as a precise figure.
/// </para>
/// </summary>
public sealed record ScanPassMeasures(
    int SystemPromptChars,
    int ConversationChars,
    int ScannerFindingsChars,
    int OpenApiDocumentChars,
    int SurfaceDifferenceChars,
    int TurnsUsed,
    int IterationCeiling,
    int DistinctReadCount);
