using System.Security.Cryptography;
using System.Text;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// p0423: reads the operator-visible facts out of a sandbox step — its argument size, the
/// one-liner naming what it touched, and the hash of the content it moved. Extracted from
/// <see cref="SandboxEventProjector"/>, which projects events; deciding what a step is
/// worth SAYING about is a different job from saying it.
/// </summary>
internal static class SandboxStepFacts
{
    // p0175-fix: one-liner for the activity row. Uses only structured fields (Path,
    // Pattern, first 1-2 Args) — never the Content blob or Env/secrets. Capped at 120
    // chars to stay readable in a row.
    private const int SummaryCap = 120;

    public static int ArgsLength(Step step)
    {
        var argsLength = 0;
        if (step.Args is { Count: > 0 })
            argsLength = step.Args.Sum(a => a?.Length ?? 0);
        if (step.Content is not null) argsLength += step.Content.Length;
        return argsLength;
    }

    public static string? Summarize(Step step) => step.Kind switch
    {
        StepKind.Run => FromArgs(step.Args),
        StepKind.ReadFile or StepKind.WriteFile or StepKind.ListFiles or StepKind.DirectoryTree
            => Trim(step.Path),
        StepKind.Grep => string.IsNullOrEmpty(step.Pattern)
            ? Trim(step.Path)
            : Trim($"{step.Pattern} in {step.Path}"),
        _ => null,
    };

    // p0369: the SHA-256 of the file content actually touched, so the run-metrics fold can
    // tell a re-read of CHANGED content (legitimate) from a re-read of unchanged content
    // (the waste signal). Read content comes from the result, written content from the
    // step; other command kinds carry no content hash.
    public static string? ContentHash(Step step, StepResult? result)
    {
        var content = step.Kind switch
        {
            StepKind.ReadFile => result?.OutputContent,
            StepKind.WriteFile => step.Content,
            _ => null,
        };
        return content is null
            ? null
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    private static string? FromArgs(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0) return null;
        var firstTwo = args.Take(2).Where(a => !string.IsNullOrEmpty(a));
        return Trim(string.Join(' ', firstTwo));
    }

    private static string? Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length > SummaryCap ? value[..SummaryCap] : value;
    }
}
