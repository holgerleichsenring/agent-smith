namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: the closed facet set of the experiential-memory store. feedback = how
/// the operator wants the agent to work; project = ongoing goals/constraints/
/// state not derivable from code or git; reference = pointers to external
/// resources.
/// </summary>
public enum MemoryEntryType
{
    Feedback,
    Project,
    Reference
}
