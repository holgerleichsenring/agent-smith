namespace AgentSmith.Domain.Entities;

/// <summary>
/// Typed in-process Diff representation. Mirrors diff.schema.json (p0128a) and is
/// what skills with output_schema=diff emit and what gets persisted to disk.
/// CodeChange remains the legacy in-flight shape consumed by existing handlers;
/// the bridge between Diff and CodeChange lands in p0128c when data flows are gated.
/// </summary>
public sealed record Diff(
    IReadOnlyList<DiffChange> Changes,
    IReadOnlyList<DiffTestEntry> TestsAdded,
    IReadOnlyList<DiffTestEntry> TestsModified,
    DiffStatus BuildStatus,
    DiffStatus TestStatus);
