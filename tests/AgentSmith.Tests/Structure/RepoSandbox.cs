using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-06-8565c: a repository in memory — files keyed by repository-relative path, read and
/// written as the sandbox agent does, and 'git ls-files -z' answered from them (or from
/// <see cref="Listing"/> when a test needs a broken one).
/// </summary>
internal sealed class RepoSandbox : ISandbox
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);
    public string? Listing { get; set; }
    public int ListingExitCode { get; set; }
    public string JobId => "repo-sandbox";

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        return Task.FromResult(step.Kind switch
        {
            StepKind.ReadFile => Files.TryGetValue(Relative(step.Path!), out var content)
                ? Result(step, 0, content) : Result(step, 1, null),
            StepKind.WriteFile => Write(step),
            StepKind.Run when step.Args?.FirstOrDefault() == "ls-files" =>
                Result(step, ListingExitCode, Listing ?? string.Join('\0', Files.Keys.Order()) + "\0\n"),
            _ => Result(step, 0, string.Empty),
        });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private StepResult Write(Step step)
    {
        Files[Relative(step.Path!)] = step.Content ?? string.Empty;
        return Result(step, 0, string.Empty);
    }

    private static string Relative(string path) =>
        path.StartsWith("/work/", StringComparison.Ordinal) ? path["/work/".Length..] : path;

    private static StepResult Result(Step step, int exit, string? output) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, exit, false, 0.01, null, output);
}
