namespace AgentSmith.Contracts.Events;

/// <summary>p0423: how a unit of work ended — the same three answers for every kind.</summary>
public enum WorkOutcome
{
    Ok,
    Failed,
    Cancelled,
}
