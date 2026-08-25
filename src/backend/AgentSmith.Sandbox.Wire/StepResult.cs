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
    /// <summary>Stated once in <see cref="WireProtocol"/>.</summary>
    public const int CurrentSchemaVersion = WireProtocol.Current;
}
