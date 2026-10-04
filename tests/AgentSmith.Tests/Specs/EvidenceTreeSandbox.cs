using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-02-3f06c: a sandbox over a fixed tree — a ReadFile of a held path answers its content,
/// any other path the reader's own "file not found". With <paramref name="materialized"/> false it
/// is a source scope that has not opened, and every step it is sent is recorded either way.
/// </summary>
internal sealed class EvidenceTreeSandbox(
    IReadOnlyDictionary<string, string> files, bool materialized = true) : ISourceScopeSandbox
{
    public List<Step> Ran { get; } = [];
    public string RepoName => "tree";
    public bool IsMaterialized => materialized;
    public string? ResolvedSha => "sha";
    public string JobId => "tree";

    public Task<string> MaterializeAsync(CancellationToken ct) => Task.FromResult("sha");

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        Ran.Add(step);
        var relative = step.Path is { } path && path.StartsWith("/work/", StringComparison.Ordinal)
            ? path["/work/".Length..]
            : step.Path ?? string.Empty;
        return Task.FromResult(files.TryGetValue(relative, out var content)
            ? new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, content)
            : new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1,
                StepErrors.FileNotFound(step.Path ?? string.Empty), null));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
