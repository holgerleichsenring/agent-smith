namespace AgentSmith.Sandbox.Agent.Models;

public sealed record Step(
    int SchemaVersion,
    Guid StepId,
    StepKind Kind = StepKind.Run,
    string? Command = null,
    IReadOnlyList<string>? Args = null,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Env = null,
    int TimeoutSeconds = Step.DefaultTimeoutSeconds)
{
    public const int CurrentSchemaVersion = 1;
    public const int DefaultTimeoutSeconds = 600;

    public static Step Shutdown(Guid id) =>
        new(CurrentSchemaVersion, id, StepKind.Shutdown);
}
