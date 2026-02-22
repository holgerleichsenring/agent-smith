using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// A single step within an execution plan.
/// </summary>
public sealed class PlanStep
{
    public int Order { get; }
    public string Description { get; }
    public FilePath? TargetFile { get; }
    public string ChangeType { get; }

    public PlanStep(int order, string description, FilePath? targetFile, string changeType)
    {
        Order = order;
        Description = description;
        TargetFile = targetFile;
        ChangeType = changeType;
    }
}
