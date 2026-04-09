namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// A group of skills that execute in the same pipeline stage.
/// Skills within a stage run sequentially but without accumulated context between them.
/// </summary>
public sealed record ExecutionStage(
    IReadOnlyList<string> Skills,
    bool IsGate,
    bool IsLead,
    bool IsExecutor = false)
{
    public string RoleLabel => IsLead ? "lead" : IsGate ? "gate" : IsExecutor ? "executor" : "contributors";
}
