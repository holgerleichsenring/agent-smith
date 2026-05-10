namespace AgentSmith.Domain.Entities;

/// <summary>
/// A test file added or modified by a Diff. Used by both
/// Diff.TestsAdded and Diff.TestsModified.
/// </summary>
public sealed record DiffTestEntry(string File, string Summary);
