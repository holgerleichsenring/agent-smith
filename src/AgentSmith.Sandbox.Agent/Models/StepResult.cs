namespace AgentSmith.Sandbox.Agent.Models;

public sealed record StepResult(
    int SchemaVersion,
    Guid StepId,
    int ExitCode,
    bool TimedOut,
    double DurationSeconds,
    string? ErrorMessage)
{
    public const int CurrentSchemaVersion = 1;
}
