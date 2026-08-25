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
    int? MaxDepth = null,
    string? Pattern = null,
    string? Glob = null,
    int? HeadLimit = null,
    int? StartLine = null,
    int? LineCount = null,
    bool WithLineNumbers = false,
    int? ContextBefore = null,
    int? ContextAfter = null,
    GrepOutputMode OutputMode = GrepOutputMode.Content,
    bool WithSizes = false,
    DirectorySortBy SortBy = DirectorySortBy.Name,
    IReadOnlyList<string>? ExcludeGlobs = null)
{
    /// <summary>The protocol version stamped on every step this build sends. Stated once in
    /// <see cref="WireProtocol"/> so three records cannot disagree about what "current" is.</summary>
    public const int CurrentSchemaVersion = WireProtocol.Current;
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
            StepKind.Grep => ValidateGrep(),
            StepKind.DirectoryTree => string.IsNullOrEmpty(Path)
                ? (false, "DirectoryTree step requires non-empty Path")
                : (true, null),
            // 2026-08-25-0d01: a kind this build cannot name is a report, not a crash. The
            // answer travels back on the result channel the server already reads, naming the
            // protocol this build speaks so the difference is legible at the other end.
            StepKind.Unknown => (false,
                $"This sandbox agent speaks wire protocol {WireProtocol.Window} and does not "
                + $"know the step kind it was sent (schema version {SchemaVersion}). Nothing "
                + "was executed."),
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

    private (bool IsValid, string? Error) ValidateGrep()
    {
        if (string.IsNullOrEmpty(Path))
            return (false, "Grep step requires non-empty Path");
        if (string.IsNullOrEmpty(Pattern))
            return (false, "Grep step requires non-empty Pattern");
        return (true, null);
    }
}

public enum GrepOutputMode
{
    Content = 0,
    FilesWithMatches = 1,
    Count = 2
}

public enum DirectorySortBy
{
    Name = 0,
    Size = 1,
    Mtime = 2
}
