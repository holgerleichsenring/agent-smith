namespace AgentSmith.Domain.Models;

/// <summary>Kind of a single line within a unified-diff hunk.</summary>
public enum PrDiffLineKind
{
    Context,
    Added,
    Removed,
}
