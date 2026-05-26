namespace AgentSmith.Domain.Entities;

/// <summary>
/// Operation flag on a DiffChange. Mirrors diff.schema.json's "operation" enum.
/// </summary>
public enum DiffOperation
{
    Modify,
    Add,
    Delete
}
