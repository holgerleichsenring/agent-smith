using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// Represents a single file change produced by the agent.
/// </summary>
public sealed class CodeChange
{
    public FilePath Path { get; }
    public string Content { get; }
    public string ChangeType { get; }

    public CodeChange(FilePath path, string content, string changeType)
    {
        Path = path;
        Content = content;
        ChangeType = changeType;
    }
}
