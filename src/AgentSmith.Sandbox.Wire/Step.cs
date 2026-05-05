namespace AgentSmith.Sandbox.Wire;

public sealed record Step(
    int SchemaVersion,
    Guid StepId,
    StepKind Kind = StepKind.Run,
    string? Command = null,
    IReadOnlyList<string>? Args = null,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Env = null,
    int TimeoutSeconds = Step.DefaultTimeoutSeconds,
    string? Path = null,
    string? Content = null,
    int? MaxDepth = null)
{
    public const int CurrentSchemaVersion = 1;
    public const int DefaultTimeoutSeconds = 600;

    public static Step Shutdown(Guid id) =>
        new(CurrentSchemaVersion, id, StepKind.Shutdown);

    public (bool IsValid, string? Error) Validate()
    {
        return Kind switch
        {
            StepKind.Run => string.IsNullOrEmpty(Command)
                ? (false, "Run step requires non-empty Command")
                : (true, null),
            StepKind.Shutdown => (true, null),
            StepKind.ReadFile => string.IsNullOrEmpty(Path)
                ? (false, "ReadFile step requires non-empty Path")
                : (true, null),
            StepKind.WriteFile => ValidateWriteFile(),
            StepKind.ListFiles => string.IsNullOrEmpty(Path)
                ? (false, "ListFiles step requires non-empty Path")
                : (true, null),
            _ => (false, $"Unknown StepKind: {Kind}")
        };
    }

    private (bool IsValid, string? Error) ValidateWriteFile()
    {
        if (string.IsNullOrEmpty(Path))
            return (false, "WriteFile step requires non-empty Path");
        if (Content is null)
            return (false, "WriteFile step requires non-null Content");
        return (true, null);
    }
}
