namespace AgentSmith.Domain.Models;

/// <summary>How a file changed within a PR diff.</summary>
public enum PrFileChangeKind
{
    Added,
    Modified,
    Deleted,
}
