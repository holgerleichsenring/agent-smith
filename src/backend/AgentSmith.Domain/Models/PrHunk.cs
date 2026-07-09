namespace AgentSmith.Domain.Models;

/// <summary>
/// One unified-diff hunk: the old/new ranges from the <c>@@ -a,b +c,d @@</c>
/// header plus the per-line breakdown with resolved line numbers.
/// </summary>
public sealed record PrHunk(
    int OldStart,
    int OldCount,
    int NewStart,
    int NewCount,
    IReadOnlyList<PrDiffLine> Lines);
