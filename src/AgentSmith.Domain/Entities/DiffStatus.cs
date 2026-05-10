namespace AgentSmith.Domain.Entities;

/// <summary>
/// Build/test status flag on a Diff. Mirrors diff.schema.json's
/// "build_status"/"test_status" enums.
/// </summary>
public enum DiffStatus
{
    Ok,
    Failed,
    NotRun
}
