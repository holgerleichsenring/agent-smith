namespace AgentSmith.Domain.Models;

/// <summary>What linking a filed child ticket to its parent through the tracker came to.</summary>
public enum ParentLinkOutcome
{
    Linked,
    Unsupported,
    Failed,
}
