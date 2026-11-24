namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: the names of the checks a PoC archive passes, as the upload answer and the import page
/// list them. A refusal names the one that failed.</summary>
public static class PocArchiveChecks
{
    public const string Paths = "paths";
    public const string Kinds = "kinds";
    public const string Bounds = "bounds";
    public const string Forbidden = "forbidden";
    public const string Dockerfile = "dockerfile";
    public const string Expose = "expose";
    public const string Binary = "binary";

    /// <summary>The checks the upload runs on declared data, in display order; <see cref="Binary"/> needs the inflated
    /// content and runs at intake only.</summary>
    public static IReadOnlyList<string> Admission { get; } = [Paths, Kinds, Bounds, Forbidden, Dockerfile, Expose];

    /// <summary>Every check the intake runs, in display order.</summary>
    public static IReadOnlyList<string> Intake { get; } = [.. Admission, Binary];
}
