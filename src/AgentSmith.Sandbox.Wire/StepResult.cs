namespace AgentSmith.Sandbox.Wire;

public sealed record StepResult(
    int SchemaVersion,
    Guid StepId,
    int ExitCode,
    bool TimedOut,
    double DurationSeconds,
    string? ErrorMessage,
    string? OutputContent = null)
{
    public const int CurrentSchemaVersion = 1;
}
